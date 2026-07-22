using KissakiSignup.Web.Domain;

namespace KissakiSignup.Web.Services;

public sealed record TeamMemberInput(int Position, int BirthYear, string RankText);

public static class TournamentRules
{
    public static RankKind ClassifyRank(string rankText)
    {
        var normalized = rankText.Trim().ToUpperInvariant();

        if (normalized.Contains("KYU", StringComparison.Ordinal))
        {
            return RankKind.Kyu;
        }

        return normalized.Contains("DAN", StringComparison.Ordinal)
            ? RankKind.Dan
            : RankKind.Unknown;
    }

    public static IReadOnlyList<RuleMessage> ValidateCompetitor(
        int birthYear,
        string rankText,
        IEnumerable<CompetitionCategory> categories,
        string idCard)
    {
        var messages = new List<RuleMessage>();

        if (string.IsNullOrWhiteSpace(idCard))
        {
            messages.Add(new RuleMessage("missing-idcard", "An id card is missing.", false));
        }

        foreach (var category in categories)
        {
            if (category == CompetitionCategory.AdultKyu && ClassifyRank(rankText) == RankKind.Dan)
            {
                messages.Add(new RuleMessage("adult-dan-blocked", "Dan ranks cannot enter the adult Kyu category.", true));
            }

            if (!IsBirthYearValidForCategory(birthYear, category))
            {
                messages.Add(new RuleMessage("category-year-mismatch", "The birth year is not eligible for this category.", true));
            }
        }

        return messages;
    }

    public static IReadOnlyList<RuleMessage> ValidateTeam(TeamType teamType, IEnumerable<TeamMemberInput> members)
    {
        var memberList = members.ToList();
        var messages = new List<RuleMessage>();

        if (memberList.Count != 3 || !memberList.Select(member => member.Position).Order().SequenceEqual([1, 2, 3]))
        {
            messages.Add(new RuleMessage("team-incomplete", "A team must contain three members in distinct positions.", false));
            return messages;
        }

        if (teamType == TeamType.Adult)
        {
            var danMembers = memberList.Where(member => ClassifyRank(member.RankText) == RankKind.Dan).ToList();
            if (danMembers.Count != 1 || danMembers[0].Position != 3)
            {
                messages.Add(new RuleMessage("adult-team-dan-position", "An adult team requires one Dan at position 3.", true));
            }

            return messages;
        }

        foreach (var member in memberList)
        {
            if (!IsBirthYearValidForYouthTeamPosition(member.BirthYear, member.Position))
            {
                messages.Add(new RuleMessage("youth-team-year-mismatch", "The birth year is not eligible for this team position.", true));
            }
        }

        return messages;
    }

    private static bool IsBirthYearValidForCategory(int birthYear, CompetitionCategory category) => category switch
    {
        CompetitionCategory.WithoutBogu => true,
        CompetitionCategory.Age7To9 => birthYear is >= 2017 and <= 2019,
        CompetitionCategory.Age10To12 => birthYear is >= 2014 and <= 2016,
        CompetitionCategory.Age13To15 => birthYear is >= 2011 and <= 2013,
        CompetitionCategory.Age16To18 => birthYear is >= 2008 and <= 2010,
        CompetitionCategory.AdultKyu => birthYear <= 2007,
        _ => false
    };

    private static bool IsBirthYearValidForYouthTeamPosition(int birthYear, int position) => position switch
    {
        1 => birthYear is >= 2017 and <= 2019,
        2 => birthYear is >= 2011 and <= 2016,
        3 => birthYear is >= 2008 and <= 2013,
        _ => false
    };
}
