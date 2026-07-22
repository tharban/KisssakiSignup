using FluentAssertions;
using KissakiSignup.Web.Services;

namespace KissakiSignup.Tests;

public class IdCardServiceTests
{
    [Theory]
    [InlineData("a-123 45", "A12345")]
    [InlineData("  de 98-76  ", "DE9876")]
    [InlineData("", "")]
    public void Normalize_ReturnsCanonicalIdCard(string? value, string expected)
    {
        IdCardService.Normalize(value).Should().Be(expected);
    }

    [Fact]
    public void Normalize_Null_ReturnsEmptyString()
    {
        IdCardService.Normalize(null).Should().Be("");
    }

    [Fact]
    public void CreateTemporaryId_UsesSubmissionPrefixAndCompetitorIndex()
    {
        IdCardService.CreateTemporaryId(Guid.Parse("11111111-1111-1111-1111-111111111111"), 3)
            .Should().Be("KISSAKI-TEMP-11111111-03");
    }
}
