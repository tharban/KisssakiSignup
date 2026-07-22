using FluentAssertions;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Services;

namespace KissakiSignup.Tests;

public class SubmissionMapperTests
{
    [Fact]
    public void CreateSubmission_MapsAndNormalizesPayload()
    {
        var submission = SubmissionMapper.CreateSubmission(CreatePayload());

        submission.Club.Name.Should().Be("Kissaki Kendo");
        submission.Club.Country.Should().Be("Germany");
        submission.Contact.Name.Should().Be("Erika Beispiel");
        submission.Competitors[0].IdCard.Should().Be("A12345");
        submission.Competitors[1].IdCardWasGenerated.Should().BeTrue();
        submission.Teams[0].Members[0].CompetitorIdCard.Should().Be("A12345");
        submission.EditToken.Length.Should().BeGreaterThan(32);
        submission.Status.Should().Be(RegistrationStatus.New);
    }

    [Fact]
    public void ApplyPayload_PreservesIdentityAndReplacesRegistrationData()
    {
        var existing = SubmissionMapper.CreateSubmission(CreatePayload());
        existing.Status = RegistrationStatus.New;
        var id = existing.Id;
        var editToken = existing.EditToken;
        var updatedAtUtc = existing.UpdatedAtUtc;
        var payload = CreatePayload();
        payload.Club.Name = " Updated Club ";
        payload.Competitors =
        [
            new CompetitorPayload { ClientId = "new", FirstName = " Anna ", LastName = " Neu ", IdCard = " b-678 90 " }
        ];
        payload.Teams = [];

        SubmissionMapper.ApplyPayload(existing, payload);

        existing.Id.Should().Be(id);
        existing.EditToken.Should().Be(editToken);
        existing.UpdatedAtUtc.Should().BeAfter(updatedAtUtc);
        existing.Status.Should().Be(RegistrationStatus.NeedsReview);
        existing.Club.Name.Should().Be("Updated Club");
        existing.Competitors.Should().ContainSingle();
        existing.Competitors[0].IdCard.Should().Be("B67890");
        existing.Teams.Should().BeEmpty();
    }

    [Fact]
    public void ToPayload_UsesIdCardAsClientId()
    {
        var submission = SubmissionMapper.CreateSubmission(CreatePayload());

        var payload = SubmissionMapper.ToPayload(submission);

        payload.Competitors[0].ClientId.Should().Be("A12345");
        payload.Teams[0].Members[0].CompetitorClientId.Should().Be("A12345");
    }

    private static RegistrationPayload CreatePayload() => new()
    {
        Club = new ClubPayload
        {
            Name = " Kissaki Kendo ", City = " Lahr ", Country = " ", Address = " ",
            Email = " info@example.org ", Phone = " ", Web = " "
        },
        Contact = new ContactPayload { Name = " Erika Beispiel ", Email = " erika@example.org ", Phone = " 123 ", Notes = " Notes " },
        Competitors =
        [
            new CompetitorPayload
            {
                ClientId = "first", FirstName = " Max ", LastName = " Mustermann ", IdCard = " a-123 45 ",
                BirthYear = 2015, RankText = " 6. Kyu ", Notes = " Note ", Categories = [CompetitionCategory.Age10To12]
            },
            new CompetitorPayload { ClientId = "second", FirstName = " Mia ", LastName = " Muster ", BirthYear = 2015 }
        ],
        Teams =
        [
            new TeamPayload
            {
                Name = " Kissaki-Team-1 ", TeamType = TeamType.Youth,
                Members = [new TeamMemberPayload { Position = 1, CompetitorClientId = " first " }]
            }
        ]
    };
}
