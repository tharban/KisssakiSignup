using System.Text.Json;
using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KissakiSignup.Web.Pages;

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
        await context.SaveChangesAsync();

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
