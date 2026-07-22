using KissakiSignup.Web.Data;
using KissakiSignup.Web.Options;
using KissakiSignup.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var tournament = builder.Configuration.GetSection(TournamentOptions.SectionName).Get<TournamentOptions>() ?? new TournamentOptions();
builder.Services.AddRazorPages();
builder.Services.Configure<TournamentOptions>(builder.Configuration.GetSection(TournamentOptions.SectionName));
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite($"Data Source={tournament.DatabasePath}"));
builder.Services.AddSingleton<CsvExportService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/admin/login";
    options.AccessDeniedPath = "/admin/login";
    options.Cookie.Name = "KissakiSignup.Admin";
    options.SlidingExpiration = true;
});
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options => options.AddFixedWindowLimiter("forms", limiterOptions =>
{
    limiterOptions.PermitLimit = 30;
    limiterOptions.Window = TimeSpan.FromMinutes(1);
    limiterOptions.QueueLimit = 0;
}));

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
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages().RequireRateLimiting("forms");
app.Run();

public partial class Program;
