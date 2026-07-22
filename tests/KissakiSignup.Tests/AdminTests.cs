using System.Net;
using System.Text.RegularExpressions;
using FluentAssertions;
using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KissakiSignup.Tests;

public class AdminTests
{
    [Fact]
    public async Task GetAdmin_AsAnonymousUser_RedirectsToLogin()
    {
        using var factory = new AdminWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/admin");

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.AbsolutePath.Should().Be("/admin/login");
    }

    [Fact]
    public async Task PostLogin_WithCorrectPasswordAndAntiforgeryToken_AuthenticatesAndShowsAdminPage()
    {
        using var factory = new AdminWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, "/admin/login");

        using var loginResponse = await client.PostAsync("/admin/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Password"] = "admin-password"
        }));

        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        loginResponse.Headers.Location!.OriginalString.Should().Be("/admin");

        using var adminResponse = await client.GetAsync("/admin");
        adminResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        (await adminResponse.Content.ReadAsStringAsync()).Should().Contain("Meldungen");
    }

    [Fact]
    public async Task PostLogin_WithEmptyConfiguredPassword_Fails()
    {
        using var factory = new AdminWebApplicationFactory(string.Empty);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, "/admin/login");

        using var response = await client.PostAsync("/admin/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Password"] = "admin-password"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Contain("Das Passwort ist nicht korrekt.");
    }

    [Fact]
    public async Task PostSubmissionStatus_AddsAdminNote()
    {
        using var factory = new AdminWebApplicationFactory();
        var submission = await SeedSubmissionAsync(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client);
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, $"/admin/submission/{submission.Id}");

        using var response = await client.PostAsync($"/admin/submission/{submission.Id}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Status"] = nameof(RegistrationStatus.NeedsReview)
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var saved = await GetSubmissionAsync(factory, submission.Id);
        saved.Status.Should().Be(RegistrationStatus.NeedsReview);
        saved.AdminNotes.Should().ContainSingle(note => note.Text == "Status geaendert auf NeedsReview.");
    }

    private static async Task LoginAsync(HttpClient client)
    {
        var antiforgeryToken = await GetAntiforgeryTokenAsync(client, "/admin/login");
        using var response = await client.PostAsync("/admin/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["Password"] = "admin-password"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
    }

    private static async Task<Submission> SeedSubmissionAsync(AdminWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var submission = SubmissionMapper.CreateSubmission(new RegistrationPayload
        {
            Club = new ClubPayload { Name = "Kissaki Kendo", City = "Lahr" },
            Contact = new ContactPayload { Name = "Erika Beispiel", Email = "erika@example.org" },
            Competitors =
            [
                new CompetitorPayload
                {
                    ClientId = "max", FirstName = "Max", LastName = "Mustermann", IdCard = "A12345",
                    BirthYear = 2015, RankText = "6. Kyu", Categories = [CompetitionCategory.Age10To12]
                }
            ]
        });
        context.Submissions.Add(submission);
        await context.SaveChangesAsync();
        return submission;
    }

    private static async Task<Submission> GetSubmissionAsync(AdminWebApplicationFactory factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Submissions
            .Include(submission => submission.AdminNotes)
            .SingleAsync(submission => submission.Id == id);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        match.Success.Should().BeTrue();
        return match.Groups[1].Value;
    }

    private sealed class AdminWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly string _adminPassword;

        public AdminWebApplicationFactory(string adminPassword = "admin-password")
        {
            _adminPassword = adminPassword;
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tournament:AdminPassword"] = _adminPassword
                });
            });

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                _connection.Dispose();
            }
        }
    }
}
