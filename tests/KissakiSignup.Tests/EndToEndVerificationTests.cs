using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Playwright;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KissakiSignup.Tests;

public class EndToEndVerificationTests
{
    private const string PaymentControlTerms =
        "ParticipationFee|PaymentStatus|payment|fee|cardNumber|bankAccount|iban|sepa|paypal|stripe|teilnahmegebuehr|teilnahmegebühr";

    [Fact]
    public async Task PublicAdminAndCsvFlow_MatchesTask10Checklist()
    {
        using var factory = new EndToEndWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var indexHtml = await client.GetStringAsync("/");
        indexHtml.Should().Contain("Kissaki Kendo Cup Anmeldung");
        indexHtml.Should().Contain("1. Verein");
        indexHtml.Should().Contain("2. Kontakt");
        indexHtml.Should().Contain("3. Teilnehmer");
        indexHtml.Should().Contain("4. Teams");
        indexHtml.Should().Contain("5. Pruefen");
        AssertWizardReachability(indexHtml);
        AssertNoPaymentControls(indexHtml);

        var registrationFormScript = await client.GetStringAsync("/js/registration-form.js");
        await AssertWizardWorksInBrowserAsync(indexHtml, registrationFormScript);

        var publicToken = GetAntiforgeryToken(indexHtml);
        using var submitResponse = await client.PostAsync("/", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = publicToken,
            ["PayloadJson"] = JsonSerializer.Serialize(CreateValidPayload()),
            ["website"] = string.Empty
        }));
        submitResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        var confirmationPath = submitResponse.Headers.Location!.OriginalString;
        confirmationPath.Should().MatchRegex("^/Confirmation/[0-9a-fA-F-]{36}$");

        var submissionId = Guid.Parse(confirmationPath.Split('/').Last());
        using var confirmationResponse = await client.GetAsync(confirmationPath);
        confirmationResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var confirmationHtml = await confirmationResponse.Content.ReadAsStringAsync();
        confirmationHtml.Should().Contain("Teilnahmegebuehr");
        AssertNoPaymentControls(confirmationHtml);
        var confirmationContent = GetConfirmationContent(confirmationHtml);
        confirmationContent.Should().NotContain("<form", "the confirmation is instruction-only");
        confirmationContent.Should().NotContain("<input", "the confirmation has no payment controls");
        confirmationContent.Should().NotContain("<select", "the confirmation has no payment controls");
        confirmationContent.Should().NotContain("<textarea", "the confirmation has no payment controls");
        confirmationContent.Should().NotContain("<button", "the confirmation has no payment controls");

        var editPath = GetPrivateEditPath(confirmationContent);
        using var editResponse = await client.GetAsync(editPath);
        editResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var editHtml = await editResponse.Content.ReadAsStringAsync();
        AssertWizardReachability(editHtml);
        editHtml.Should().Contain("window.initialRegistrationPayload");

        using var anonymousClient = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        using var anonymousAdminResponse = await anonymousClient.GetAsync("/admin");
        anonymousAdminResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        anonymousAdminResponse.Headers.Location!.AbsolutePath.Should().Be("/admin/login");

        var loginHtml = await client.GetStringAsync("/admin/login");
        using var loginResponse = await client.PostAsync("/admin/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = GetAntiforgeryToken(loginHtml),
            ["Password"] = "task10-admin"
        }));
        loginResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);

        var adminHtml = await client.GetStringAsync("/admin");
        adminHtml.Should().Contain("Meldungen");
        adminHtml.Should().Contain("Kissaki Kendo");

        var detailPath = $"/admin/submission/{submissionId}";
        var detailHtml = await client.GetStringAsync(detailPath);
        detailHtml.Should().Contain("Max Mustermann");
        detailHtml.Should().Contain("Kissaki-Team-1");

        using var reviewedResponse = await client.PostAsync(detailPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = GetAntiforgeryToken(detailHtml),
            ["Status"] = nameof(RegistrationStatus.Reviewed)
        }));
        reviewedResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await GetSubmissionStatusAsync(factory, submissionId)).Should().Be(RegistrationStatus.Reviewed);

        var clubsBytes = await client.GetByteArrayAsync("/admin/export/clubs.csv");
        var participantsBytes = await client.GetByteArrayAsync("/admin/export/participants.csv");
        var teamsBytes = await client.GetByteArrayAsync("/admin/export/teams.csv");
        AssertCsv(clubsBytes, "#name;country;city;address;email;phone;web");
        AssertCsv(participantsBytes, "#Name;Lastname;idCard;Club;ClubCity").Should().Contain("A12345");
        AssertCsv(teamsBytes, "#name;tournament;member1;member2;member3;member4;member5;member6;member7;member8;member9")
            .Should().Contain("A12345");

        detailHtml = await client.GetStringAsync(detailPath);
        using var disabledResponse = await client.PostAsync(detailPath, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = GetAntiforgeryToken(detailHtml),
            ["Status"] = nameof(RegistrationStatus.Disabled)
        }));
        disabledResponse.StatusCode.Should().Be(HttpStatusCode.Redirect);
        (await GetSubmissionStatusAsync(factory, submissionId)).Should().Be(RegistrationStatus.Disabled);
    }

    private static RegistrationPayload CreateValidPayload() => new()
    {
        Club = new ClubPayload
        {
            Name = "Kissaki Kendo",
            City = "Lahr",
            Country = "Germany",
            Email = "info@example.org"
        },
        Contact = new ContactPayload { Name = "Erika Beispiel", Email = "erika@example.org" },
        Competitors =
        [
            new CompetitorPayload
            {
                ClientId = "c1",
                FirstName = "Max",
                LastName = "Mustermann",
                IdCard = "a-123 45",
                BirthYear = 1990,
                RankText = "2. Kyu",
                HasBogu = true,
                Categories = [CompetitionCategory.AdultKyu]
            },
            new CompetitorPayload
            {
                ClientId = "c2",
                FirstName = "Mia",
                LastName = "Musterfrau",
                IdCard = "B67890",
                BirthYear = 1992,
                RankText = "1. Kyu",
                HasBogu = true,
                Categories = [CompetitionCategory.AdultKyu]
            },
            new CompetitorPayload
            {
                ClientId = "c3",
                FirstName = "Kai",
                LastName = "Dan",
                IdCard = "C24680",
                BirthYear = 1988,
                RankText = "1. Dan",
                HasBogu = true,
                Categories = []
            }
        ],
        Teams =
        [
            new TeamPayload
            {
                Name = "Kissaki-Team-1",
                TeamType = TeamType.Adult,
                Members =
                [
                    new TeamMemberPayload { Position = 1, CompetitorClientId = "c1" },
                    new TeamMemberPayload { Position = 2, CompetitorClientId = "c2" },
                    new TeamMemberPayload { Position = 3, CompetitorClientId = "c3" }
                ]
            }
        ]
    };

    private static string GetAntiforgeryToken(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
        match.Success.Should().BeTrue();
        return match.Groups[1].Value;
    }

    private static string AssertCsv(byte[] bytes, string expectedHeader)
    {
        bytes.Should().StartWith([0xEF, 0xBB, 0xBF]);
        var csv = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetString(bytes[3..]);
        csv.Should().StartWith(expectedHeader);
        return csv;
    }

    private static void AssertWizardReachability(string html)
    {
        for (var step = 0; step < 5; step++)
        {
            Regex.IsMatch(html, $"<section[^>]*data-step=\"{step}\"", RegexOptions.IgnoreCase)
                .Should().BeTrue($"wizard step {step} must be reachable");
        }

        html.Should().Contain("id=\"previous-step\"");
        html.Should().Contain("id=\"next-step\"");
        html.Should().Contain("id=\"submit-registration\"");
        html.Should().Contain("js/registration-form.js");
    }

    private static void AssertWizardScriptWiring(string script)
    {
        script.Should().Contain("const steps = Array.from(document.querySelectorAll(\"[data-step]\"))");
        script.Should().Contain("previousButton.addEventListener(\"click\"");
        script.Should().Contain("nextButton.addEventListener(\"click\"");
        script.Should().Contain("form.addEventListener(\"submit\"");
        script.Should().Contain("document.getElementById(\"payload-json\").value = JSON.stringify(state)");
        script.Should().Contain("renderCompetitors();");
        script.Should().Contain("renderTeams();");
        script.Should().Contain("showStep(0);");
        script.Should().Contain("if (currentStep === steps.length - 1) renderReview()");
    }

    private static void AssertNoPaymentControls(string html)
    {
        var paymentControlPattern = $"<(?:input|select|textarea|button|a)\\b(?=[^>]*(?:{PaymentControlTerms}))[^>]*>";
        Regex.IsMatch(html, paymentControlPattern, RegexOptions.IgnoreCase).Should().BeFalse();
    }

    private static async Task AssertWizardWorksInBrowserAsync(string html, string registrationFormScript)
    {
        var edgePath = @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe";
        File.Exists(edgePath).Should().BeTrue("Task 10 browser verification uses the installed Microsoft Edge browser");

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            ExecutablePath = edgePath,
            Headless = true
        });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(html);
        await page.AddScriptTagAsync(new PageAddScriptTagOptions { Content = registrationFormScript });

        await AssertOnlyStepVisibleAsync(page, 0);
        await page.FillAsync("#club-name", "Kissaki Kendo");
        await page.FillAsync("#club-city", "Lahr");
        await page.ClickAsync("#next-step");
        await AssertOnlyStepVisibleAsync(page, 1);

        await page.FillAsync("#contact-name", "Erika Beispiel");
        await page.FillAsync("#contact-email", "erika@example.org");
        await page.ClickAsync("#next-step");
        await AssertOnlyStepVisibleAsync(page, 2);

        await page.ClickAsync("#add-competitor");
        (await page.Locator("[data-competitor-index='0']").IsVisibleAsync()).Should().BeTrue();
        await page.FillAsync("[data-competitor-index='0'] [data-property='firstName']", "Max");
        await page.FillAsync("[data-competitor-index='0'] [data-property='lastName']", "Mustermann");
        await page.ClickAsync("#next-step");
        await AssertOnlyStepVisibleAsync(page, 3);

        await page.ClickAsync("#add-team");
        (await page.Locator("[data-team-index='0']").IsVisibleAsync()).Should().BeTrue();
        await page.ClickAsync("#next-step");
        await AssertOnlyStepVisibleAsync(page, 4);
        (await page.Locator("#submit-registration").IsVisibleAsync()).Should().BeTrue();
        (await page.Locator("#review").TextContentAsync()).Should().Contain("Kissaki Kendo");
        (await page.Locator("#review").TextContentAsync()).Should().Contain("Max Mustermann");

        await page.ClickAsync("#previous-step");
        await AssertOnlyStepVisibleAsync(page, 3);
        await page.ClickAsync("#next-step");
        await AssertOnlyStepVisibleAsync(page, 4);

        await page.EvaluateAsync("document.getElementById('registration-form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))");
        var payloadJson = await page.InputValueAsync("#payload-json");
        payloadJson.Should().Contain("\"Kissaki Kendo\"");
        payloadJson.Should().Contain("\"Max\"");

        var paymentControls = await page.Locator("input, select, textarea, button, a").EvaluateAllAsync<string[]>(
            @"elements => elements
                .map(element => Array.from(element.attributes).map(attribute => `${attribute.name}=${attribute.value}`).join(' '))
                .filter(attributes => /ParticipationFee|PaymentStatus|payment|fee|cardNumber|bankAccount|iban|sepa|paypal|stripe|teilnahmegebuehr|teilnahmegebühr/i.test(attributes))");
        paymentControls.Should().BeEmpty();
    }

    private static async Task AssertOnlyStepVisibleAsync(IPage page, int visibleStep)
    {
        for (var step = 0; step < 5; step++)
        {
            var isVisible = await page.Locator($"[data-step='{step}']").IsVisibleAsync();
            isVisible.Should().Be(step == visibleStep, $"step {visibleStep} should be the visible wizard step");
        }
    }

    private static string GetPrivateEditPath(string confirmationHtml)
    {
        Regex.Matches(confirmationHtml, "<a\\b", RegexOptions.IgnoreCase)
            .Should().HaveCount(1, "the confirmation has no links beyond the private edit link");
        var editLinks = Regex.Matches(confirmationHtml, "<a\\s+href=\"(?<href>/edit/[^\"]+)\"[^>]*>", RegexOptions.IgnoreCase);
        editLinks.Should().ContainSingle("confirmation must expose exactly one private edit link");
        return editLinks[0].Groups["href"].Value;
    }

    private static string GetConfirmationContent(string confirmationHtml)
    {
        var match = Regex.Match(confirmationHtml, "<div class=\"form-shell\">(?<content>.*?)</div>", RegexOptions.Singleline);
        match.Success.Should().BeTrue();
        return match.Groups["content"].Value;
    }

    private static async Task<RegistrationStatus> GetSubmissionStatusAsync(
        EndToEndWebApplicationFactory factory,
        Guid id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.Submissions
            .Where(submission => submission.Id == id)
            .Select(submission => submission.Status)
            .SingleAsync();
    }

    private sealed class EndToEndWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");

        public EndToEndWebApplicationFactory()
        {
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Tournament:AdminPassword"] = "task10-admin",
                    ["Tournament:RegistrationOpen"] = "true",
                    ["Tournament:RegistrationDeadline"] = DateTime.UtcNow.AddYears(10).ToString("yyyy-MM-dd"),
                    ["Tournament:TournamentName"] = "Kissaki Cup 2026"
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
