using KissakiSignup.Web.Data;
using KissakiSignup.Web.Options;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var tournament = builder.Configuration.GetSection(TournamentOptions.SectionName).Get<TournamentOptions>() ?? new TournamentOptions();
builder.Services.AddRazorPages();
builder.Services.Configure<TournamentOptions>(builder.Configuration.GetSection(TournamentOptions.SectionName));
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={tournament.DatabasePath}"));

var app = builder.Build();
await DbInitializer.InitializeAsync(app.Services);
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.Run();

public partial class Program;
