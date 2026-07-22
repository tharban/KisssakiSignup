using System.Security.Cryptography;
using KissakiSignup.Web.Domain;

namespace KissakiSignup.Web.Services;

public static class SubmissionMapper
{
    public static Submission CreateSubmission(RegistrationPayload payload)
    {
        var submission = new Submission
        {
            Id = Guid.NewGuid(),
            EditToken = CreateEditToken(),
            Status = RegistrationStatus.New,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };

        ApplyPayloadData(submission, payload);
        return submission;
    }

    public static void ApplyPayload(Submission existing, RegistrationPayload payload)
    {
        ApplyPayloadData(existing, payload);
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        existing.Status = RegistrationStatus.NeedsReview;
    }

    public static RegistrationPayload ToPayload(Submission submission) => new()
    {
        Club = new ClubPayload
        {
            Name = submission.Club.Name, City = submission.Club.City, Country = submission.Club.Country,
            Address = submission.Club.Address, Email = submission.Club.Email, Phone = submission.Club.Phone, Web = submission.Club.Web
        },
        Contact = new ContactPayload
        {
            Name = submission.Contact.Name, Email = submission.Contact.Email, Phone = submission.Contact.Phone, Notes = submission.Contact.Notes
        },
        Competitors = submission.Competitors.Select(competitor => new CompetitorPayload
        {
            ClientId = competitor.IdCard, FirstName = competitor.FirstName, LastName = competitor.LastName, IdCard = competitor.IdCard,
            BirthYear = competitor.BirthYear, RankText = competitor.RankText, HasBogu = competitor.HasBogu, Notes = competitor.Notes,
            Categories = competitor.Categories.Select(category => category.Category).ToList()
        }).ToList(),
        Teams = submission.Teams.Select(team => new TeamPayload
        {
            Name = team.Name, TeamType = team.TeamType,
            Members = team.Members.Select(member => new TeamMemberPayload
            {
                Position = member.Position, CompetitorClientId = member.CompetitorIdCard
            }).ToList()
        }).ToList()
    };

    private static void ApplyPayloadData(Submission submission, RegistrationPayload payload)
    {
        ApplyClub(submission.Club, payload.Club);
        ApplyContact(submission.Contact, payload.Contact);

        var idCardsByClientId = new Dictionary<string, string>(StringComparer.Ordinal);
        submission.Competitors = payload.Competitors.Select((competitor, index) =>
        {
            var idCard = IdCardService.Normalize(competitor.IdCard);
            var wasGenerated = string.IsNullOrEmpty(idCard);
            idCard = wasGenerated ? IdCardService.CreateTemporaryId(submission.Id, index) : idCard;
            idCardsByClientId[Trim(competitor.ClientId)] = idCard;

            return new Competitor
            {
                SubmissionId = submission.Id,
                FirstName = Trim(competitor.FirstName),
                LastName = Trim(competitor.LastName),
                IdCard = idCard,
                IdCardWasGenerated = wasGenerated,
                BirthYear = competitor.BirthYear,
                RankText = Trim(competitor.RankText),
                HasBogu = competitor.HasBogu,
                Notes = Trim(competitor.Notes),
                Categories = competitor.Categories.Select(category => new CompetitorCategory { Category = category }).ToList()
            };
        }).ToList();

        submission.Teams = payload.Teams.Select(team => new Team
        {
            SubmissionId = submission.Id,
            Name = Trim(team.Name),
            TeamType = team.TeamType,
            Members = team.Members.Select(member => new TeamMember
            {
                Position = member.Position,
                CompetitorIdCard = idCardsByClientId.GetValueOrDefault(Trim(member.CompetitorClientId), Trim(member.CompetitorClientId))
            }).ToList()
        }).ToList();
    }

    private static void ApplyClub(Club club, ClubPayload payload)
    {
        club.Name = Trim(payload.Name);
        club.City = Trim(payload.City);
        club.Country = string.IsNullOrWhiteSpace(payload.Country) ? "Germany" : Trim(payload.Country);
        club.Address = Trim(payload.Address);
        club.Email = Trim(payload.Email);
        club.Phone = Trim(payload.Phone);
        club.Web = Trim(payload.Web);
    }

    private static void ApplyContact(Contact contact, ContactPayload payload)
    {
        contact.Name = Trim(payload.Name);
        contact.Email = Trim(payload.Email);
        contact.Phone = Trim(payload.Phone);
        contact.Notes = Trim(payload.Notes);
    }

    private static string CreateEditToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
}
