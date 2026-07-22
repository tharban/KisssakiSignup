namespace KissakiSignup.Web.Domain;

public enum RegistrationStatus
{
    Draft = 1,
    Submitted = 2,
    Exported = 3,
    NeedsReview = 4,
    Disabled = 5
}

public enum CompetitionCategory
{
    WithoutBogu = 1,
    Age7To9 = 2,
    Age10To12 = 3,
    Age13To15 = 4,
    Age16To18 = 5,
    AdultKyu = 6
}

public enum TeamType
{
    Youth = 1,
    Adult = 2
}

public enum RankKind
{
    Unknown = 0,
    Kyu = 1,
    Dan = 2
}

public sealed record RuleMessage(string Code, string Text, bool IsBlocking);
