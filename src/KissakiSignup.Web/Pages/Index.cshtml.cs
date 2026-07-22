using System.Text.Json;
using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Options;
using KissakiSignup.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace KissakiSignup.Web.Pages;

[RequestSizeLimit(RegistrationPayloadLimits.MaxRequestBodyBytes)]
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
        if (PayloadJson.Length > RegistrationPayloadLimits.MaxPayloadJsonLength)
        {
            ModelState.AddModelError(string.Empty, "The registration data is too large.");
            return Page();
        }

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

    public static IReadOnlyList<RuleMessage> Validate(RegistrationPayload payload) => RegistrationPayloadValidator.Validate(payload);
}
