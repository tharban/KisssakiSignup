using FluentAssertions;
using KissakiSignup.Web.Domain;
using KissakiSignup.Web.Services;

namespace KissakiSignup.Tests;

public class TournamentRulesTests
{
    [Theory]
    [InlineData("1. Kyu", RankKind.Kyu)]
    [InlineData("1. Dan", RankKind.Dan)]
    public void ClassifyRank_ReturnsExpectedRankKind(string rankText, RankKind expected)
    {
        TournamentRules.ClassifyRank(rankText).Should().Be(expected);
    }

    [Fact]
    public void ValidateCompetitor_BlocksDanRankInAdultKyu()
    {
        TournamentRules.ValidateCompetitor(1990, "1. Dan", [CompetitionCategory.AdultKyu], "A12345")
            .Should().Contain(message => message.Code == "adult-dan-blocked" && message.IsBlocking);
    }

    [Fact]
    public void ValidateCompetitor_BlocksCategoryOutsideYearRange()
    {
        TournamentRules.ValidateCompetitor(2014, "6. Kyu", [CompetitionCategory.Age7To9], "B12345")
            .Should().Contain(message => message.Code == "category-year-mismatch" && message.IsBlocking);
    }

    [Fact]
    public void ValidateCompetitor_WarnsWhenIdCardIsMissing()
    {
        TournamentRules.ValidateCompetitor(2016, "6. Kyu", [CompetitionCategory.Age10To12], "")
            .Should().Contain(message => message.Code == "missing-idcard" && !message.IsBlocking);
    }

    [Fact]
    public void ValidateTeam_BlocksAdultTeamWithDanOutsidePositionThree()
    {
        TournamentRules.ValidateTeam(
                TeamType.Adult,
                [new TeamMemberInput(1, 1990, "2. Kyu"), new TeamMemberInput(2, 1992, "1. Dan"), new TeamMemberInput(3, 1988, "1. Kyu")])
            .Should().Contain(message => message.Code == "adult-team-dan-position" && message.IsBlocking);
    }

    [Fact]
    public void ValidateTeam_WarnsWhenRequiredPositionsAreIncomplete()
    {
        TournamentRules.ValidateTeam(
                TeamType.Youth,
                [new TeamMemberInput(1, 2018, "6. Kyu"), new TeamMemberInput(2, 2014, "6. Kyu"), new TeamMemberInput(4, 2010, "6. Kyu")])
            .Should().Contain(message => message.Code == "team-incomplete" && !message.IsBlocking);
    }
}
