using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace KissakiSignup.Web.Pages.Admin;

[Authorize]
public class ExportModel(ApplicationDbContext context, CsvExportService csvExport) : PageModel
{
    public async Task<IActionResult> OnGetAsync(string file)
    {
        var submissions = await context.Submissions
            .Where(submission => submission.Status != RegistrationStatus.Disabled)
            .AsNoTracking()
            .AsSplitQuery()
            .Include(submission => submission.Club)
            .Include(submission => submission.Competitors)
            .ThenInclude(competitor => competitor.Categories)
            .Include(submission => submission.Teams)
            .ThenInclude(team => team.Members)
            .ToListAsync();

        return file switch
        {
            "clubs.csv" => File(csvExport.ExportClubs(submissions), "text/csv; charset=utf-8", "clubs.csv"),
            "participants.csv" => File(csvExport.ExportParticipants(submissions), "text/csv; charset=utf-8", "participants.csv"),
            "teams.csv" => File(csvExport.ExportTeams(submissions), "text/csv; charset=utf-8", "teams.csv"),
            _ => NotFound()
        };
    }
}
