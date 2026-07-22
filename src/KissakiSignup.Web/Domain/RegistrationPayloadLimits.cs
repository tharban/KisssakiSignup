namespace KissakiSignup.Web.Domain;

public static class RegistrationPayloadLimits
{
    // These limits keep anonymous form posts small while allowing normal club registrations.
    public const int MaxRequestBodyBytes = 128 * 1024;
    public const int MaxPayloadJsonLength = 64 * 1024;
    public const int MaxCompetitors = 100;
    public const int MaxTeams = 50;
    public const int MaxTeamMembers = 3;
    public const int MaxCategoriesPerCompetitor = 6;
    public const int MaxNameLength = 120;
    public const int MaxCityLength = 100;
    public const int MaxCountryLength = 80;
    public const int MaxAddressLength = 250;
    public const int MaxEmailLength = 254;
    public const int MaxPhoneLength = 50;
    public const int MaxWebLength = 2048;
    public const int MaxNotesLength = 1000;
    public const int MaxClientIdLength = 128;
    public const int MaxIdCardLength = 64;
    public const int MaxRankLength = 64;
}
