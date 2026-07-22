namespace KissakiSignup.Web.Options;

public sealed class TournamentOptions
{
    public const string SectionName = "Tournament";
    public string TournamentName { get; init; } = "Kissaki Cup 2026";
    public DateOnly TournamentDate { get; init; } = new(2026, 10, 25);
    public DateOnly RegistrationDeadline { get; init; } = new(2026, 10, 11);
    public bool RegistrationOpen { get; init; } = true;
    public string DatabasePath { get; init; } = "App_Data/kissaki-registration.sqlite";
    public string AdminPassword { get; init; } = "";
}
