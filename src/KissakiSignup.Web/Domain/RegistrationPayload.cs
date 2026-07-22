namespace KissakiSignup.Web.Domain;

public class RegistrationPayload
{
    public ClubPayload Club { get; set; } = new();
    public ContactPayload Contact { get; set; } = new();
    public List<CompetitorPayload> Competitors { get; set; } = [];
    public List<TeamPayload> Teams { get; set; } = [];
}

public class ClubPayload
{
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Web { get; set; } = string.Empty;
}

public class ContactPayload
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class CompetitorPayload
{
    public string ClientId { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdCard { get; set; } = string.Empty;
    public int BirthYear { get; set; }
    public string RankText { get; set; } = string.Empty;
    public bool HasBogu { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<CompetitionCategory> Categories { get; set; } = [];
}

public class TeamPayload
{
    public string Name { get; set; } = string.Empty;
    public TeamType TeamType { get; set; }
    public List<TeamMemberPayload> Members { get; set; } = [];
}

public class TeamMemberPayload
{
    public int Position { get; set; }
    public string CompetitorClientId { get; set; } = string.Empty;
}
