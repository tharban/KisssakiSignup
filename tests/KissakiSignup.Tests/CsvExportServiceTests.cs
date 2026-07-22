using System.Text;
using FluentAssertions;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Options;
using KissakiSignup.Web.Services;
using Microsoft.Extensions.Options;

namespace KissakiSignup.Tests;

public class CsvExportServiceTests
{
    private readonly CsvExportService service = new(Options.Create(new TournamentOptions { TournamentName = "Kissaki Cup 2026" }));

    [Fact]
    public void ExportClubs_WritesBomHeaderAndDeduplicatedClubs()
    {
        var first = CreateSubmission();
        var duplicate = CreateSubmission();
        duplicate.Club.Name = " kissaki kendo ";
        duplicate.Club.City = " lahr ";

        var bytes = service.ExportClubs([first, duplicate]);

        bytes.Take(Encoding.UTF8.Preamble.Length).ToArray().Should().Equal(Encoding.UTF8.Preamble.ToArray());
        GetLines(bytes).Should().Equal(
            "#name;country;city;address;email;phone;web",
            "Kissaki Kendo;Germany;Lahr;;info@example.org;;");
    }

    [Fact]
    public void ExportParticipants_WritesExpectedColumnsAndEscapesCsvFields()
    {
        var submission = CreateSubmission();
        submission.Competitors[0].FirstName = "Max; \"M\"";
        submission.Competitors = [submission.Competitors[0]];

        var bytes = service.ExportParticipants([submission]);

        GetLines(bytes).Should().Equal(
            "#Name;Lastname;idCard;Club;ClubCity",
            "\"Max; \"\"M\"\"\";Mustermann;A12345;Kissaki Kendo;Lahr");
    }

    [Fact]
    public void ExportTeams_WritesTournamentAndExactlyNineMemberColumns()
    {
        var bytes = service.ExportTeams([CreateSubmission()]);

        GetLines(bytes).Should().Equal(
            "#name;tournament;member1;member2;member3;member4;member5;member6;member7;member8;member9",
            "Kissaki-Team-1;Kissaki Cup 2026;A12345;B67890;C24680;;;;;;");
    }

    [Fact]
    public void ExportParticipants_SkipsDisabledSubmissions()
    {
        var disabled = CreateSubmission();
        disabled.Status = RegistrationStatus.Disabled;

        var bytes = service.ExportParticipants([disabled]);

        GetLines(bytes).Should().Equal("#Name;Lastname;idCard;Club;ClubCity");
    }

    private static string[] GetLines(byte[] bytes) => Encoding.UTF8.GetString(bytes)
        .TrimStart('\uFEFF')
        .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);

    private static Submission CreateSubmission() => new()
    {
        Status = RegistrationStatus.New,
        Club = new Club { Name = "Kissaki Kendo", Country = "Germany", City = "Lahr", Email = "info@example.org" },
        Competitors =
        [
            new Competitor { FirstName = "Max", LastName = "Mustermann", IdCard = "A12345" },
            new Competitor { FirstName = "Mia", LastName = "Muster", IdCard = "B67890" },
            new Competitor { FirstName = "Noah", LastName = "Muster", IdCard = "C24680" }
        ],
        Teams =
        [
            new Team
            {
                Name = "Kissaki-Team-1",
                Members =
                [
                    new TeamMember { Position = 1, CompetitorIdCard = "A12345" },
                    new TeamMember { Position = 2, CompetitorIdCard = "B67890" },
                    new TeamMember { Position = 3, CompetitorIdCard = "C24680" }
                ]
            }
        ]
    };
}
