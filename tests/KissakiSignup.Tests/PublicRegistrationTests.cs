using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Pages;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KissakiSignup.Tests;

public class PublicRegistrationTests
{
    [Fact]
    public async Task GetIndex_ShowsRegistrationFormWithoutPaymentFields()
    {
        using var factory = new RegistrationWebApplicationFactory();
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        html.Should().Contain("Kissaki Kendo Cup Anmeldung");
        html.Should().NotContain("name=\"ParticipationFee\"");
        html.Should().NotContain("name=\"PaymentStatus\"");
        html.Should().NotContain("name=\"payment\"");
    }

    [Fact]
    public async Task PostIndex_WithValidPayloadAndAntiforgeryToken_RedirectsToConfirmation()
    {
        using var factory = new RegistrationWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var antiforgeryToken = await GetAntiforgeryToken(client, "/");
        var payload = CreateValidPayload();

        using var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["PayloadJson"] = JsonSerializer.Serialize(payload),
            ["website"] = string.Empty
        }));

        response.StatusCode.Should().Be(HttpStatusCode.Redirect);
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.OriginalString.Should().MatchRegex("^/Confirmation/[0-9a-fA-F-]{36}$");
        (await CountSubmissionsAsync(factory)).Should().Be(1);
    }

    [Fact]
    public async Task PostIndex_WithHoneypotValue_DoesNotSaveSubmission()
    {
        using var factory = new RegistrationWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var antiforgeryToken = await GetAntiforgeryToken(client, "/");

        using var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["PayloadJson"] = JsonSerializer.Serialize(CreateValidPayload()),
            ["website"] = "https://spam.example"
        }));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CountSubmissionsAsync(factory)).Should().Be(0);
    }

    [Fact]
    public async Task PostIndex_WithNullNestedPayloadValues_ReturnsValidationErrors()
    {
        await AssertMalformedPayloadReturnsValidationErrorsAsync(
            "{\"club\":null,\"contact\":null,\"competitors\":null,\"teams\":[{\"members\":null}]}",
            expectsInvalidEntryError: false);
    }

    [Fact]
    public async Task PostIndex_WithNullListElements_ReturnsValidationErrors()
    {
        await AssertMalformedPayloadReturnsValidationErrorsAsync(
            "{\"club\":null,\"contact\":null,\"competitors\":[null],\"teams\":[null,{\"members\":[null]}]}",
            expectsInvalidEntryError: true);
    }

    [Fact]
    public async Task PostIndex_WithDuplicateNormalizedIdCards_ReturnsValidationErrorWithoutSaving()
    {
        var payload = CreateValidPayload();
        payload.Competitors.Add(new CompetitorPayload
        {
            ClientId = "mia",
            FirstName = "Mia",
            LastName = "Muster",
            IdCard = "a 12345",
            BirthYear = 2015,
            RankText = "6. Kyu",
            Categories = [CompetitionCategory.Age10To12]
        });

        await AssertPostIndexValidationErrorAsync(payload, "Each non-empty id card must be unique.");
    }

    [Fact]
    public async Task PostIndex_WithBlankCompetitorNamesAndInvalidContactEmail_ReturnsValidationErrorsWithoutSaving()
    {
        var payload = CreateValidPayload();
        payload.Contact.Email = "not-an-email";
        payload.Competitors[0].FirstName = " ";
        payload.Competitors[0].LastName = " ";

        await AssertPostIndexValidationErrorAsync(payload, "Competitor first name is required.");
        await AssertPostIndexValidationErrorAsync(payload, "Contact email must be a valid email address.");
    }

    [Fact]
    public async Task PostIndex_WithInvalidEnumValues_ReturnsValidationErrorsWithoutSaving()
    {
        const string payloadJson = """
            {"club":{"name":"Kissaki Kendo","city":"Lahr"},"contact":{"name":"Erika Beispiel","email":"erika@example.org"},"competitors":[{"clientId":"max","firstName":"Max","lastName":"Mustermann","idCard":"A12345","birthYear":2015,"rankText":"6. Kyu","categories":[999]}],"teams":[{"name":"Invalid Team","teamType":999,"members":[]}]}
            """;

        await AssertPostIndexValidationErrorAsync(payloadJson, "The selected competition category is invalid.");
        await AssertPostIndexValidationErrorAsync(payloadJson, "The selected team type is invalid.");
    }

    [Fact]
    public async Task PostIndex_WithTooManyCompetitors_ReturnsValidationErrorWithoutSaving()
    {
        var payload = CreateValidPayload();
        payload.Competitors = Enumerable.Range(0, 101).Select(index => new CompetitorPayload
        {
            ClientId = $"competitor-{index}",
            FirstName = "Max",
            LastName = $"Mustermann {index}",
            IdCard = $"A{index:00000}",
            BirthYear = 2015,
            RankText = "6. Kyu",
            Categories = [CompetitionCategory.Age10To12]
        }).ToList();

        await AssertPostIndexValidationErrorAsync(payload, "A registration can contain no more than 100 competitors.");
    }

    [Fact]
    public async Task Validate_RejectsUnknownAndDuplicateTeamMemberReferences()
    {
        var payload = CreateValidPayload();
        payload.Teams =
        [
            new TeamPayload
            {
                Name = "Team", TeamType = TeamType.Youth,
                Members =
                [
                    new TeamMemberPayload { Position = 1, CompetitorClientId = "unknown" },
                    new TeamMemberPayload { Position = 2, CompetitorClientId = "max" },
                    new TeamMemberPayload { Position = 3, CompetitorClientId = "max" }
                ]
            }
        ];

        var messages = IndexModel.Validate(payload);

        messages.Should().Contain(message => message.Code == "team-member-unknown" && message.IsBlocking);
        messages.Should().Contain(message => message.Code == "team-member-duplicate" && message.IsBlocking);
    }

    [Fact]
    public void Validate_EmptyTeamMemberPlaceholdersProduceOnlyIncompleteTeamWarning()
    {
        var payload = CreateValidPayload();
        payload.Teams =
        [
            new TeamPayload
            {
                Name = "Team", TeamType = TeamType.Youth,
                Members =
                [
                    new TeamMemberPayload { Position = 1, CompetitorClientId = "" },
                    new TeamMemberPayload { Position = 2, CompetitorClientId = "" },
                    new TeamMemberPayload { Position = 3, CompetitorClientId = "" }
                ]
            }
        ];

        var messages = IndexModel.Validate(payload);

        messages.Should().Contain(message => message.Code == "team-incomplete" && !message.IsBlocking);
        messages.Should().NotContain(message => message.Code == "youth-team-year-mismatch");
        messages.Should().NotContain(message => message.Code == "adult-team-dan-position");
    }

    private static async Task AssertMalformedPayloadReturnsValidationErrorsAsync(string payloadJson, bool expectsInvalidEntryError)
    {
        using var factory = new RegistrationWebApplicationFactory();
        using var client = factory.CreateClient();
        var antiforgeryToken = await GetAntiforgeryToken(client, "/");

        using var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["PayloadJson"] = payloadJson,
            ["website"] = string.Empty
        }));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain("Club name is required.");
        html.Should().Contain("Contact name is required.");
        html.Should().Contain("At least one competitor is required.");
        if (expectsInvalidEntryError)
        {
            html.Should().Contain("The registration data contains invalid entries.");
        }
        (await CountSubmissionsAsync(factory)).Should().Be(0);
    }

    private static Task AssertPostIndexValidationErrorAsync(RegistrationPayload payload, string expectedError) =>
        AssertPostIndexValidationErrorAsync(JsonSerializer.Serialize(payload), expectedError);

    private static async Task AssertPostIndexValidationErrorAsync(string payloadJson, string expectedError)
    {
        using var factory = new RegistrationWebApplicationFactory();
        using var client = factory.CreateClient();
        var antiforgeryToken = await GetAntiforgeryToken(client, "/");

        using var response = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = antiforgeryToken,
            ["PayloadJson"] = payloadJson,
            ["website"] = string.Empty
        }));
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        html.Should().Contain(expectedError);
        (await CountSubmissionsAsync(factory)).Should().Be(0);
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

    private static async Task<int> CountSubmissionsAsync(RegistrationWebApplicationFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Submissions.CountAsync();
    }

    private static async Task<string> GetAntiforgeryToken(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        match.Success.Should().BeTrue();
        return match.Groups[1].Value;
    }

    private sealed class RegistrationWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public RegistrationWebApplicationFactory()
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
