using System.Net;
using System.Text.Json;
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

public class EditRegistrationTests
{
    [Fact]
    public async Task GetEdit_WithUnknownToken_ReturnsNotFound()
    {
        using var factory = new EditRegistrationWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/edit/unknown-token");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostEdit_WithValidPayloadAndAntiforgeryToken_UpdatesSubmissionAndRedirectsToConfirmation()
    {
        using var factory = new EditRegistrationWebApplicationFactory();
        var submission = await SeedSubmissionAsync(factory);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var antiforgeryToken = await GetAntiforgeryToken(client, $"/edit/{submission.EditToken}");
        var payload = SubmissionMapper.ToPayload(submission);
        payload.Club.Name = "Updated Kissaki Kendo";

        using var response = await client.PostAsync($"/edit/{submission.EditToken}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["PayloadJson"] = JsonSerializer.Serialize(payload)
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.Should().Be($"/Confirmation/{submission.Id}");
        (await GetSubmissionAsync(factory, submission.Id)).Club.Name.Should().Be("Updated Kissaki Kendo");
    }

    [Fact]
    public async Task PostEdit_WithUnreadablePayload_RendersValidationErrorAndPersistedPayload()
    {
        using var factory = new EditRegistrationWebApplicationFactory();
        var submission = await SeedSubmissionAsync(factory);
        using var client = factory.CreateClient();
        var antiforgeryToken = await GetAntiforgeryToken(client, $"/edit/{submission.EditToken}");

        using var response = await client.PostAsync($"/edit/{submission.EditToken}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["PayloadJson"] = "{not-json"
        }));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("The registration data could not be read.");
        html.Should().Contain("window.initialRegistrationPayload = {\"club\"");
    }

    [Fact]
    public async Task PostEdit_ForDisabledSubmission_PreservesDisabledStatusAndExcludesItFromExports()
    {
        using var factory = new EditRegistrationWebApplicationFactory();
        var submission = await SeedSubmissionAsync(factory);
        await SetStatusAsync(factory, submission.Id, RegistrationStatus.Disabled);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var antiforgeryToken = await GetAntiforgeryToken(client, $"/edit/{submission.EditToken}");
        var payload = SubmissionMapper.ToPayload(submission);
        payload.Club.Name = "Edited Disabled Club";

        using var response = await client.PostAsync($"/edit/{submission.EditToken}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["PayloadJson"] = JsonSerializer.Serialize(payload)
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var persisted = await GetSubmissionAsync(factory, submission.Id);
        persisted.Status.Should().Be(RegistrationStatus.Disabled);

        using var scope = factory.Services.CreateScope();
        var exporter = scope.ServiceProvider.GetRequiredService<CsvExportService>();
        var csv = System.Text.Encoding.UTF8.GetString(exporter.ExportParticipants([persisted]));
        csv.Should().NotContain("Max;Mustermann");
    }

    private static async Task<Submission> SeedSubmissionAsync(EditRegistrationWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var submission = SubmissionMapper.CreateSubmission(CreateValidPayload());
        context.Submissions.Add(submission);
        await context.SaveChangesAsync();
        return submission;
    }

    private static async Task<Submission> GetSubmissionAsync(EditRegistrationWebApplicationFactory factory, Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Submissions
            .Include(submission => submission.Club)
            .Include(submission => submission.Competitors)
            .SingleAsync(submission => submission.Id == id);
    }

    private static async Task SetStatusAsync(EditRegistrationWebApplicationFactory factory, Guid id, RegistrationStatus status)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var submission = await context.Submissions.SingleAsync(submission => submission.Id == id);
        submission.Status = status;
        await context.SaveChangesAsync();
    }

    private static async Task<string> GetAntiforgeryToken(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        match.Success.Should().BeTrue();
        return match.Groups[1].Value;
    }

    private static RegistrationPayload CreateValidPayload() => new()
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
    };

    private sealed class EditRegistrationWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public EditRegistrationWebApplicationFactory()
        {
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tournament:RegistrationOpen"] = "true",
                    ["Tournament:RegistrationDeadline"] = "2026-10-11"
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
