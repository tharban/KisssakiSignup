using System.Text.Json;
using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KissakiSignup.Web.Pages;

[RequestSizeLimit(RegistrationPayloadLimits.MaxRequestBodyBytes)]
public class EditModel(ApplicationDbContext context) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    [BindProperty]
    public string PayloadJson { get; set; } = string.Empty;

    public string InitialPayloadJson { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string token)
    {
        var submission = await LoadSubmissionAsync(token);
        if (submission is null)
        {
            return NotFound();
        }

        InitialPayloadJson = JsonSerializer.Serialize(SubmissionMapper.ToPayload(submission), JsonOptions);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string token)
    {
        var submission = await LoadSubmissionAsync(token);
        if (submission is null)
        {
            return NotFound();
        }

        InitialPayloadJson = JsonSerializer.Serialize(SubmissionMapper.ToPayload(submission), JsonOptions);

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

        InitialPayloadJson = JsonSerializer.Serialize(payload, JsonOptions);
        foreach (var message in IndexModel.Validate(payload).Where(message => message.IsBlocking))
        {
            ModelState.AddModelError(string.Empty, message.Text);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        SubmissionMapper.ApplyPayload(submission, payload);
        var currentStatus = await context.Submissions
            .AsNoTracking()
            .Where(existing => existing.Id == submission.Id)
            .Select(existing => existing.Status)
            .SingleAsync();
        if (currentStatus == RegistrationStatus.Disabled)
        {
            submission.Status = RegistrationStatus.Disabled;
        }

        try
        {
            await context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            context.ChangeTracker.Clear();
            var latestSubmission = await context.Submissions
                .AsNoTracking()
                .SingleOrDefaultAsync(existing => existing.Id == submission.Id);
            if (latestSubmission is null)
            {
                return NotFound();
            }

            if (latestSubmission.Status == RegistrationStatus.Disabled)
            {
                return RedirectToPage("/Confirmation", new { id = latestSubmission.Id });
            }

            ModelState.AddModelError(string.Empty, "The registration was changed by another request. Please review and submit it again.");
            return Page();
        }

        return RedirectToPage("/Confirmation", new { id = submission.Id });
    }

    private Task<Submission?> LoadSubmissionAsync(string token) => context.Submissions
        .Include(submission => submission.Club)
        .Include(submission => submission.Contact)
        .Include(submission => submission.Competitors)
        .ThenInclude(competitor => competitor.Categories)
        .Include(submission => submission.Teams)
        .ThenInclude(team => team.Members)
        .SingleOrDefaultAsync(submission => submission.EditToken == token);
}
