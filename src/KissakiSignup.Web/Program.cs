using KissakiSignup.Web.Data;
using KissakiSignup.Web.Options;
using KissakiSignup.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);
var tournament = builder.Configuration.GetSection(TournamentOptions.SectionName).Get<TournamentOptions>() ?? new TournamentOptions();
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Admin/Export", "/admin/export/{file}");
});
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
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("forms", httpContext =>
    {
        var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var path = string.Concat(httpContext.Request.PathBase.Value, httpContext.Request.Path.Value).ToUpperInvariant();
        var partitionKey = $"{clientIp}:{httpContext.Request.Method}:{path}";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 30,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

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
