namespace KissakiSignup.Web.Services;

public static class IdCardService
{
    public static string Normalize(string? value) => string.IsNullOrWhiteSpace(value)
        ? string.Empty
        : value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    public static string CreateTemporaryId(Guid submissionId, int competitorIndex)
    {
        var prefix = submissionId.ToString("N")[..8].ToUpperInvariant();
        return $"KISSAKI-TEMP-{prefix}-{competitorIndex:00}";
    }
}
