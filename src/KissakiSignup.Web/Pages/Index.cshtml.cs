using System.Text.Json;
using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Options;
using KissakiSignup.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KissakiSignup.Web.Pages;

public class IndexModel(ApplicationDbContext context, IOptions<TournamentOptions> tournamentOptions) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [BindProperty]
    public string PayloadJson { get; set; } = string.Empty;

    [BindProperty(Name = "website")]
    public string? Website { get; set; }

    public RegistrationPayload? InitialRegistrationPayload { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!string.IsNullOrWhiteSpace(Website))
        {
            return new NoContentResult();
        }

        var tournament = tournamentOptions.Value;
        if (!tournament.RegistrationOpen || DateOnly.FromDateTime(DateTime.UtcNow) > tournament.RegistrationDeadline)
        {
            ModelState.AddModelError(string.Empty, "Registration is closed.");
            return Page();
        }

        RegistrationPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<RegistrationPayload>(PayloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            ModelState.AddModelError(string.Empty, "The registration data could not be read.");
            return Page();
        }

        if (payload is null)
        {
            ModelState.AddModelError(string.Empty, "The registration data could not be read.");
            return Page();
        }

        InitialRegistrationPayload = payload;
        foreach (var message in Validate(payload).Where(message => message.IsBlocking))
        {
            ModelState.AddModelError(string.Empty, message.Text);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var submission = SubmissionMapper.CreateSubmission(payload);
        context.Submissions.Add(submission);
        await context.SaveChangesAsync();

        return RedirectToPage("/Confirmation", new { id = submission.Id });
    }

    public static IReadOnlyList<RuleMessage> Validate(RegistrationPayload payload)
    {
        var hasNullEntries = NormalizeNestedValues(payload);
        var messages = new List<RuleMessage>();

        if (hasNullEntries)
        {
            messages.Add(new RuleMessage("invalid-registration-data", "The registration data contains invalid entries.", true));
        }

        if (string.IsNullOrWhiteSpace(payload.Club.Name))
        {
            messages.Add(new RuleMessage("club-name-required", "Club name is required.", true));
        }

        if (string.IsNullOrWhiteSpace(payload.Club.City))
        {
            messages.Add(new RuleMessage("club-city-required", "Club city is required.", true));
        }

        if (string.IsNullOrWhiteSpace(payload.Contact.Name))
        {
            messages.Add(new RuleMessage("contact-name-required", "Contact name is required.", true));
        }

        if (string.IsNullOrWhiteSpace(payload.Contact.Email))
        {
            messages.Add(new RuleMessage("contact-email-required", "Contact email is required.", true));
        }

        if (payload.Competitors.Count == 0)
        {
            messages.Add(new RuleMessage("competitor-required", "At least one competitor is required.", true));
        }

        foreach (var competitor in payload.Competitors)
        {
            messages.AddRange(TournamentRules.ValidateCompetitor(
                competitor.BirthYear,
                competitor.RankText,
                competitor.Categories,
                competitor.IdCard));
        }

        foreach (var team in payload.Teams)
        {
            var members = team.Members.Select(member =>
            {
                var competitor = payload.Competitors.FirstOrDefault(candidate =>
                    string.Equals(candidate.ClientId, member.CompetitorClientId, StringComparison.Ordinal));

                return new TeamMemberInput(
                    member.Position,
                    competitor?.BirthYear ?? 0,
                    competitor?.RankText ?? string.Empty);
            });

            messages.AddRange(TournamentRules.ValidateTeam(team.TeamType, members));
        }

        return messages;
    }

    private static bool NormalizeNestedValues(RegistrationPayload payload)
    {
        var hasNullEntries = false;
        payload.Club ??= new ClubPayload();
        payload.Contact ??= new ContactPayload();
        payload.Competitors ??= [];
        payload.Teams ??= [];

        if (payload.Competitors.RemoveAll(competitor => competitor is null) > 0)
        {
            hasNullEntries = true;
        }

        if (payload.Teams.RemoveAll(team => team is null) > 0)
        {
            hasNullEntries = true;
        }

        foreach (var competitor in payload.Competitors)
        {
            competitor.Categories ??= [];
        }

        foreach (var team in payload.Teams)
        {
            team.Members ??= [];

            if (team.Members.RemoveAll(member => member is null) > 0)
            {
                hasNullEntries = true;
            }
        }

        return hasNullEntries;
    }
}
