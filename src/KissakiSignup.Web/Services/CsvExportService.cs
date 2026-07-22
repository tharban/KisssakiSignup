using System.Text;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Options;
using Microsoft.Extensions.Options;

namespace KissakiSignup.Web.Services;

public sealed class CsvExportService(IOptions<TournamentOptions> tournamentOptions)
{
    private readonly string tournamentName = tournamentOptions.Value.TournamentName;

    public byte[] ExportClubs(IEnumerable<Submission> submissions)
    {
        var clubs = ActiveSubmissions(submissions)
            .Select(submission => submission.Club)
            .GroupBy(club => (NormalizeClubPart(club.Name), NormalizeClubPart(club.City)))
            .Select(group => group.First());

        return CreateCsv(
            ["#name", "country", "city", "address", "email", "phone", "web"],
            clubs.Select(club => new[] { club.Name, club.Country, club.City, club.Address, club.Email, club.Phone, club.Web }));
    }

    public byte[] ExportParticipants(IEnumerable<Submission> submissions) => CreateCsv(
        ["#Name", "Lastname", "idCard", "Club", "ClubCity"],
        ActiveSubmissions(submissions).SelectMany(submission => submission.Competitors.Select(competitor => new[]
        {
            competitor.FirstName, competitor.LastName, competitor.IdCard, submission.Club.Name, submission.Club.City
        })));

    public byte[] ExportTeams(IEnumerable<Submission> submissions) => CreateCsv(
        ["#name", "tournament", "member1", "member2", "member3", "member4", "member5", "member6", "member7", "member8", "member9"],
        ActiveSubmissions(submissions).SelectMany(submission => submission.Teams.Select(team =>
        {
            var members = Enumerable.Range(1, 9)
                .Select(position => team.Members.FirstOrDefault(member => member.Position == position)?.CompetitorIdCard ?? string.Empty);
            return new[] { team.Name, tournamentName }.Concat(members);
        })));

    private static IEnumerable<Submission> ActiveSubmissions(IEnumerable<Submission> submissions) =>
        submissions.Where(submission => submission.Status != RegistrationStatus.Disabled);

    private static byte[] CreateCsv(IEnumerable<string> header, IEnumerable<IEnumerable<string>> rows)
    {
        var builder = new StringBuilder();
        WriteRow(builder, header);
        foreach (var row in rows)
        {
            WriteRow(builder, row);
        }

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        return encoding.GetPreamble().Concat(encoding.GetBytes(builder.ToString())).ToArray();
    }

    private static void WriteRow(StringBuilder builder, IEnumerable<string> fields)
    {
        builder.AppendJoin(';', fields.Select(Escape));
        builder.Append("\r\n");
    }

    private static string Escape(string? field)
    {
        var value = field ?? string.Empty;
        return value.IndexOfAny([';', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static string NormalizeClubPart(string value) => value.Trim().ToUpperInvariant();
}
