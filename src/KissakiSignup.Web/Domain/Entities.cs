namespace KissakiSignup.Web.Domain;

public class Submission
{
    public Guid Id { get; set; }
    public string EditToken { get; set; } = string.Empty;
    public RegistrationStatus Status { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public DateTimeOffset? ExportedAtUtc { get; set; }
    public Club Club { get; set; } = new();
    public Contact Contact { get; set; } = new();
    public List<Competitor> Competitors { get; set; } = [];
    public List<Team> Teams { get; set; } = [];
    public List<AdminNote> AdminNotes { get; set; } = [];
}

public class Club
{
    public int Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Web { get; set; } = string.Empty;
}

public class Contact
{
    public int Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
}

public class Competitor
{
    public int Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string IdCard { get; set; } = string.Empty;
    public bool IdCardWasGenerated { get; set; }
    public int BirthYear { get; set; }
    public string RankText { get; set; } = string.Empty;
    public bool HasBogu { get; set; }
    public string Notes { get; set; } = string.Empty;
    public List<CompetitorCategory> Categories { get; set; } = [];
}

public class CompetitorCategory
{
    public int Id { get; set; }
    public int CompetitorId { get; set; }
    public CompetitionCategory Category { get; set; }
}

public class Team
{
    public int Id { get; set; }
    public Guid SubmissionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TeamType TeamType { get; set; }
    public List<TeamMember> Members { get; set; } = [];
}

public class TeamMember
{
    public int Id { get; set; }
    public int TeamId { get; set; }
    public int Position { get; set; }
    public string CompetitorIdCard { get; set; } = string.Empty;
}

public class AdminNote
{
    public int Id { get; set; }
    public Guid SubmissionId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string Text { get; set; } = string.Empty;
}
