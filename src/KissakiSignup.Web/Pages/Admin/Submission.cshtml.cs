using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KissakiSignup.Web.Pages.Admin;

[Authorize]
public class SubmissionModel(ApplicationDbContext context) : PageModel
{
    public static readonly RegistrationStatus[] StatusOptions =
    [
        RegistrationStatus.New,
        RegistrationStatus.NeedsReview,
        RegistrationStatus.Reviewed,
        RegistrationStatus.Exported,
        RegistrationStatus.Disabled
    ];

    public Submission Submission { get; private set; } = new();

    [BindProperty]
    public RegistrationStatus Status { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var submission = await LoadSubmissionAsync(id);
        if (submission is null)
        {
            return NotFound();
        }

        Submission = submission;
        Status = submission.Status;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        var submission = await LoadSubmissionAsync(id);
        if (submission is null)
        {
            return NotFound();
        }

        if (!StatusOptions.Contains(Status))
        {
            Submission = submission;
            ModelState.AddModelError(nameof(Status), "Der Status ist ungültig.");
            return Page();
        }

        if (submission.Status != Status)
        {
            submission.Status = Status;
            submission.UpdatedAtUtc = DateTimeOffset.UtcNow;
            submission.AdminNotes.Add(new AdminNote
            {
                SubmissionId = submission.Id,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                Text = $"Status geaendert auf {Status}."
            });
            await context.SaveChangesAsync();
        }

        return RedirectToPage(new { id });
    }

    private Task<Submission?> LoadSubmissionAsync(Guid id) => context.Submissions
        .Include(submission => submission.Club)
        .Include(submission => submission.Contact)
        .Include(submission => submission.Competitors)
        .ThenInclude(competitor => competitor.Categories)
        .Include(submission => submission.Teams)
        .ThenInclude(team => team.Members)
        .Include(submission => submission.AdminNotes)
        .SingleOrDefaultAsync(submission => submission.Id == id);
}
