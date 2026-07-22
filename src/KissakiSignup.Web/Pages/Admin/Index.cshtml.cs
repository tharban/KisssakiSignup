using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KissakiSignup.Web.Pages.Admin;

[Authorize]
public class IndexModel(ApplicationDbContext context) : PageModel
{
    public IReadOnlyList<Submission> Submissions { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Submissions = await context.Submissions
            .FromSqlRaw("SELECT * FROM \"Submissions\" ORDER BY \"UpdatedAtUtc\" DESC")
            .AsNoTracking()
            .AsSplitQuery()
            .Include(submission => submission.Club)
            .Include(submission => submission.Competitors)
            .ToListAsync();
    }
}
