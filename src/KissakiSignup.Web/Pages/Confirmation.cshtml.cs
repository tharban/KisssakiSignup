using KissakiSignup.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KissakiSignup.Web.Pages;

public class ConfirmationModel(ApplicationDbContext context) : PageModel
{
    public string EditToken { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var submission = await context.Submissions
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == id);

        if (submission is null)
        {
            return NotFound();
        }

        EditToken = submission.EditToken;
        return Page();
    }
}
