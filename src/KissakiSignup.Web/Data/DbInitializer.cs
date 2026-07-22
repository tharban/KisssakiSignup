using KissakiSignup.Web.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KissakiSignup.Web.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var tournament = scope.ServiceProvider.GetRequiredService<IOptions<TournamentOptions>>().Value;
        var databaseDirectory = Path.GetDirectoryName(Path.GetFullPath(tournament.DatabasePath));

        if (databaseDirectory is not null)
        {
            Directory.CreateDirectory(databaseDirectory);
        }

        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }
}
