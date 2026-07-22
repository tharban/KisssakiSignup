# Kissaki Anmeldung Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a public no-login registration web app for the 4. Kissaki Kendo Cup 2026 with admin review and KendoTournamentManager CSV export.

**Architecture:** ASP.NET Core Razor Pages app with focused domain services for tournament rules, idCard normalization, persistence, and CSV export. SQLite stores submissions on disk. Public club managers submit without login; admins authenticate with one configured password and export three KTM CSV files.

**Tech Stack:** .NET 10 LTS, ASP.NET Core Razor Pages, EF Core SQLite, xUnit, FluentAssertions, Microsoft.AspNetCore.Mvc.Testing, Windows/IIS deployment without Docker.

## Global Constraints

- Club managers submit through a public form without login.
- The public form must not collect participation fee, payment status, or online payment data.
- After successful submission, show a payment instruction note only.
- Admin area uses one server-configured password.
- Primary deployment target is a small Azure Windows VM with IIS and .NET Hosting Bundle.
- Do not introduce Docker, containers, Kubernetes, or a separate database server.
- Store app data in SQLite at configurable path `App_Data/kissaki-registration.sqlite`.
- Default tournament name for KTM export is `Kissaki Cup 2026`.
- Default tournament date is `2026-10-25`.
- Default registration deadline is `2026-10-11`.
- Export `clubs.csv`, `participants.csv`, and `teams.csv` as semicolon-separated UTF-8 with BOM.
- Normalize `idCard` by trimming, removing spaces and hyphens, and uppercasing.
- Do not expose a public list of registrations.

---

## File Structure

- `KissakiSignup.sln`: solution file for web app and tests.
- `src/KissakiSignup.Web/KissakiSignup.Web.csproj`: Razor Pages web project.
- `src/KissakiSignup.Web/Program.cs`: service registration, middleware, database migration, admin auth, rate limiting.
- `src/KissakiSignup.Web/appsettings.json`: defaults for tournament settings and SQLite path.
- `src/KissakiSignup.Web/Options/TournamentOptions.cs`: strongly typed tournament settings.
- `src/KissakiSignup.Web/Domain/Enums.cs`: registration status, categories, team type, rank kind.
- `src/KissakiSignup.Web/Domain/Entities.cs`: EF Core entities.
- `src/KissakiSignup.Web/Domain/RegistrationPayload.cs`: DTO posted by public and edit forms.
- `src/KissakiSignup.Web/Services/IdCardService.cs`: idCard normalization and temporary ID generation.
- `src/KissakiSignup.Web/Services/TournamentRules.cs`: category, rank, and team validation.
- `src/KissakiSignup.Web/Services/SubmissionMapper.cs`: converts form DTOs into entities and back.
- `src/KissakiSignup.Web/Services/CsvExportService.cs`: KTM CSV generation.
- `src/KissakiSignup.Web/Data/ApplicationDbContext.cs`: EF Core mapping.
- `src/KissakiSignup.Web/Data/DbInitializer.cs`: creates data directory and applies migrations.
- `src/KissakiSignup.Web/Pages/Index.cshtml`: public registration wizard.
- `src/KissakiSignup.Web/Pages/Index.cshtml.cs`: handles public submission and shared validation.
- `src/KissakiSignup.Web/Pages/Edit.cshtml`: private edit wizard.
- `src/KissakiSignup.Web/Pages/Edit.cshtml.cs`: loads and updates registration by token.
- `src/KissakiSignup.Web/Pages/Confirmation.cshtml`: confirmation and payment note.
- `src/KissakiSignup.Web/Pages/Admin/*`: admin login, list, detail, and export endpoints.
- `src/KissakiSignup.Web/wwwroot/js/registration-form.js`: dynamic wizard state.
- `src/KissakiSignup.Web/wwwroot/css/site.css`: restrained form/admin styling.
- `tests/KissakiSignup.Tests/*.cs`: unit and integration tests.
- `docs/deployment/windows-iis.md`: Windows/IIS deployment and backup guide.

---

### Task 1: Solution Scaffold And Runtime Configuration

**Files:**
- Create: `KissakiSignup.sln`
- Create: `src/KissakiSignup.Web/KissakiSignup.Web.csproj`
- Create: `tests/KissakiSignup.Tests/KissakiSignup.Tests.csproj`
- Create: `src/KissakiSignup.Web/Options/TournamentOptions.cs`
- Modify: `src/KissakiSignup.Web/Program.cs`
- Modify: `src/KissakiSignup.Web/appsettings.json`
- Create: `.gitignore`

**Interfaces:**
- Produces: `TournamentOptions` with `TournamentName`, `TournamentDate`, `RegistrationDeadline`, `RegistrationOpen`, `DatabasePath`, `AdminPassword`.

- [ ] **Step 1: Create solution and projects**

```powershell
dotnet new sln -n KissakiSignup
dotnet new webapp -n KissakiSignup.Web -o src/KissakiSignup.Web --framework net10.0
dotnet new xunit -n KissakiSignup.Tests -o tests/KissakiSignup.Tests --framework net10.0
dotnet sln KissakiSignup.sln add src/KissakiSignup.Web/KissakiSignup.Web.csproj
dotnet sln KissakiSignup.sln add tests/KissakiSignup.Tests/KissakiSignup.Tests.csproj
dotnet add tests/KissakiSignup.Tests/KissakiSignup.Tests.csproj reference src/KissakiSignup.Web/KissakiSignup.Web.csproj
```

Expected: `dotnet sln KissakiSignup.sln list` shows both projects.

- [ ] **Step 2: Add packages**

```powershell
dotnet add src/KissakiSignup.Web/KissakiSignup.Web.csproj package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src/KissakiSignup.Web/KissakiSignup.Web.csproj package Microsoft.EntityFrameworkCore.Design
dotnet add tests/KissakiSignup.Tests/KissakiSignup.Tests.csproj package FluentAssertions
dotnet add tests/KissakiSignup.Tests/KissakiSignup.Tests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/KissakiSignup.Tests/KissakiSignup.Tests.csproj package Microsoft.EntityFrameworkCore.Sqlite
```

Expected: `dotnet restore KissakiSignup.sln` succeeds.

- [ ] **Step 3: Add `.gitignore`**

```gitignore
bin/
obj/
.vs/
.vscode/
artifacts/
src/KissakiSignup.Web/App_Data/
src/KissakiSignup.Web/wwwroot/exports/
*.sqlite
*.sqlite-shm
*.sqlite-wal
```

- [ ] **Step 4: Add `TournamentOptions`**

```csharp
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
```

- [ ] **Step 5: Configure defaults in `appsettings.json`**

```json
{
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } },
  "AllowedHosts": "*",
  "Tournament": {
    "TournamentName": "Kissaki Cup 2026",
    "TournamentDate": "2026-10-25",
    "RegistrationDeadline": "2026-10-11",
    "RegistrationOpen": true,
    "DatabasePath": "App_Data/kissaki-registration.sqlite",
    "AdminPassword": ""
  }
}
```

- [ ] **Step 6: Wire options in `Program.cs`**

```csharp
using KissakiSignup.Web.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.Configure<TournamentOptions>(builder.Configuration.GetSection(TournamentOptions.SectionName));
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
public partial class Program;
```

- [ ] **Step 7: Verify and commit**

```powershell
dotnet build KissakiSignup.sln
dotnet test KissakiSignup.sln
git add KissakiSignup.sln .gitignore src tests
git commit -m "chore: scaffold signup app"
```

---

### Task 2: Domain Rules And idCard Normalization

**Files:**
- Create: `src/KissakiSignup.Web/Domain/Enums.cs`
- Create: `src/KissakiSignup.Web/Services/IdCardService.cs`
- Create: `src/KissakiSignup.Web/Services/TournamentRules.cs`
- Create: `tests/KissakiSignup.Tests/IdCardServiceTests.cs`
- Create: `tests/KissakiSignup.Tests/TournamentRulesTests.cs`

**Interfaces:**
- Produces: `IdCardService.Normalize(string? value): string`.
- Produces: `IdCardService.CreateTemporaryId(Guid submissionId, int competitorIndex): string`.
- Produces: `TournamentRules.ClassifyRank(string rankText): RankKind`.
- Produces: `TournamentRules.ValidateCompetitor(...)` and `TournamentRules.ValidateTeam(...)`.

- [ ] **Step 1: Write failing idCard tests**

Test cases:

```csharp
IdCardService.Normalize("a-123 45").Should().Be("A12345");
IdCardService.Normalize("  de 98-76  ").Should().Be("DE9876");
IdCardService.Normalize("").Should().Be("");
IdCardService.Normalize(null).Should().Be("");
IdCardService.CreateTemporaryId(Guid.Parse("11111111-1111-1111-1111-111111111111"), 3).Should().Be("KISSAKI-TEMP-11111111-03");
```

Run:

```powershell
dotnet test tests/KissakiSignup.Tests/KissakiSignup.Tests.csproj --filter IdCardServiceTests
```

Expected: FAIL because service does not exist.

- [ ] **Step 2: Add enums and rule message**

Create enums for `RegistrationStatus`, `CompetitionCategory`, `TeamType`, `RankKind`, and record `RuleMessage(string Code, string Text, bool IsBlocking)`.

Exact category values:

```csharp
WithoutBogu = 1,
Age7To9 = 2,
Age10To12 = 3,
Age13To15 = 4,
Age16To18 = 5,
AdultKyu = 6
```

- [ ] **Step 3: Implement idCard service**

```csharp
public static string Normalize(string? value) => string.IsNullOrWhiteSpace(value)
    ? string.Empty
    : value.Trim().Replace(" ", string.Empty, StringComparison.Ordinal).Replace("-", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

public static string CreateTemporaryId(Guid submissionId, int competitorIndex)
{
    var prefix = submissionId.ToString("N")[..8].ToUpperInvariant();
    return $"KISSAKI-TEMP-{prefix}-{competitorIndex:00}";
}
```

- [ ] **Step 4: Write failing tournament rule tests**

Cover these exact behaviours:

```csharp
TournamentRules.ClassifyRank("1. Kyu").Should().Be(RankKind.Kyu);
TournamentRules.ClassifyRank("1. Dan").Should().Be(RankKind.Dan);
TournamentRules.ValidateCompetitor(1990, "1. Dan", [CompetitionCategory.AdultKyu], "A12345").Should().Contain(m => m.Code == "adult-dan-blocked" && m.IsBlocking);
TournamentRules.ValidateCompetitor(2014, "6. Kyu", [CompetitionCategory.Age7To9], "B12345").Should().Contain(m => m.Code == "category-year-mismatch" && m.IsBlocking);
TournamentRules.ValidateCompetitor(2016, "6. Kyu", [CompetitionCategory.Age10To12], "").Should().Contain(m => m.Code == "missing-idcard" && !m.IsBlocking);
TournamentRules.ValidateTeam(TeamType.Adult, [new TeamMemberInput(1, 1990, "2. Kyu"), new TeamMemberInput(2, 1992, "1. Dan"), new TeamMemberInput(3, 1988, "1. Kyu")]).Should().Contain(m => m.Code == "adult-team-dan-position" && m.IsBlocking);
```

- [ ] **Step 5: Implement tournament rules**

Rules:

- `Kyu` if normalized rank contains `KYU`.
- `Dan` if normalized rank contains `DAN`.
- `AdultKyu` blocks Dan ranks.
- Year ranges: 7-9 = 2017-2019, 10-12 = 2014-2016, 13-15 = 2011-2013, 16-18 = 2008-2010, adult = 2007 and older.
- Youth team position 1 accepts 2017-2019.
- Youth team position 2 accepts 2011-2016.
- Youth team position 3 accepts 2008-2013.
- Adult team requires exactly one Dan at position 3.
- Incomplete teams create non-blocking `team-incomplete`.

- [ ] **Step 6: Verify and commit**

```powershell
dotnet test KissakiSignup.sln
git add src/KissakiSignup.Web/Domain src/KissakiSignup.Web/Services tests/KissakiSignup.Tests
git commit -m "feat: add tournament rules"
```

---

### Task 3: Persistence Model And Database Initialization

**Files:**
- Create: `src/KissakiSignup.Web/Domain/Entities.cs`
- Create: `src/KissakiSignup.Web/Domain/RegistrationPayload.cs`
- Create: `src/KissakiSignup.Web/Data/ApplicationDbContext.cs`
- Create: `src/KissakiSignup.Web/Data/DbInitializer.cs`
- Modify: `src/KissakiSignup.Web/Program.cs`
- Create: `tests/KissakiSignup.Tests/PersistenceTests.cs`

**Interfaces:**
- Produces EF entities: `Submission`, `Club`, `Contact`, `Competitor`, `CompetitorCategory`, `Team`, `TeamMember`, `AdminNote`.
- Produces payload DTOs: `RegistrationPayload`, `ClubPayload`, `ContactPayload`, `CompetitorPayload`, `TeamPayload`, `TeamMemberPayload`.
- Produces `ApplicationDbContext`.

- [ ] **Step 1: Write failing persistence test**

Use in-memory SQLite and prove the app can save one submission with club, contact, one competitor, one category, one team, and one team member. Query it back with `Include` / `ThenInclude` and assert:

```csharp
saved.Club.Name.Should().Be("Kissaki Kendo");
saved.Competitors.Single().Categories.Single().Category.Should().Be(CompetitionCategory.Age10To12);
saved.Teams.Single().Members.Single().CompetitorIdCard.Should().Be("A12345");
```

Expected: FAIL because persistence classes do not exist.

- [ ] **Step 2: Add entities**

Create entities with these required properties:

```csharp
Submission: Guid Id, string EditToken, RegistrationStatus Status, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? ExportedAtUtc, Club Club, Contact Contact, List<Competitor> Competitors, List<Team> Teams, List<AdminNote> AdminNotes
Club: int Id, Guid SubmissionId, string Name, City, Country, Address, Email, Phone, Web
Contact: int Id, Guid SubmissionId, string Name, Email, Phone, Notes
Competitor: int Id, Guid SubmissionId, string FirstName, LastName, IdCard, bool IdCardWasGenerated, int BirthYear, string RankText, bool HasBogu, string Notes, List<CompetitorCategory> Categories
CompetitorCategory: int Id, int CompetitorId, CompetitionCategory Category
Team: int Id, Guid SubmissionId, string Name, TeamType TeamType, List<TeamMember> Members
TeamMember: int Id, int TeamId, int Position, string CompetitorIdCard
AdminNote: int Id, Guid SubmissionId, DateTimeOffset CreatedAtUtc, string Text
```

- [ ] **Step 3: Add payload DTOs**

Payloads mirror the public form. `CompetitorPayload.ClientId` is used in the browser to connect teams to competitors before database IDs exist.

- [ ] **Step 4: Add `ApplicationDbContext` mapping**

Mappings:

- `Submission.EditToken` has unique index.
- `Submission` owns one `Club` and one `Contact` via cascade delete.
- `Submission` has many `Competitors`, `Teams`, and `AdminNotes` via cascade delete.
- `Competitor` has many `CompetitorCategory` via cascade delete.
- `Team` has many `TeamMember` via cascade delete.
- Unique index on `(SubmissionId, IdCard)` for competitors.

- [ ] **Step 5: Add database initializer and migration**

`DbInitializer.InitializeAsync(IServiceProvider)` creates the directory for `TournamentOptions.DatabasePath` and calls `Database.MigrateAsync()`.

Commands:

```powershell
dotnet new tool-manifest
dotnet tool install dotnet-ef
dotnet ef migrations add InitialCreate --project src/KissakiSignup.Web/KissakiSignup.Web.csproj --startup-project src/KissakiSignup.Web/KissakiSignup.Web.csproj --output-dir Data/Migrations
```

- [ ] **Step 6: Wire SQLite in `Program.cs`**

Register `ApplicationDbContext` with:

```csharp
options.UseSqlite($"Data Source={tournament.DatabasePath}");
```

Call:

```csharp
await DbInitializer.InitializeAsync(app.Services);
```

before middleware.

- [ ] **Step 7: Verify and commit**

```powershell
dotnet test KissakiSignup.sln
git add .config src/KissakiSignup.Web tests/KissakiSignup.Tests
git commit -m "feat: add registration persistence"
```

---

### Task 4: Submission Mapping And KTM CSV Export

**Files:**
- Create: `src/KissakiSignup.Web/Services/SubmissionMapper.cs`
- Create: `src/KissakiSignup.Web/Services/CsvExportService.cs`
- Modify: `src/KissakiSignup.Web/Program.cs`
- Create: `tests/KissakiSignup.Tests/SubmissionMapperTests.cs`
- Create: `tests/KissakiSignup.Tests/CsvExportServiceTests.cs`

**Interfaces:**
- Produces: `SubmissionMapper.CreateSubmission(RegistrationPayload payload): Submission`.
- Produces: `SubmissionMapper.ApplyPayload(Submission existing, RegistrationPayload payload): void`.
- Produces: `SubmissionMapper.ToPayload(Submission submission): RegistrationPayload`.
- Produces: `CsvExportService.ExportClubs`, `ExportParticipants`, `ExportTeams`, each returning `byte[]`.

- [ ] **Step 1: Write mapper tests**

Assert all of these:

```csharp
submission.Club.Name.Should().Be("Kissaki Kendo");
submission.Club.Country.Should().Be("Germany");
submission.Competitors[0].IdCard.Should().Be("A12345");
submission.Competitors[1].IdCardWasGenerated.Should().BeTrue();
submission.Teams[0].Members[0].CompetitorIdCard.Should().Be("A12345");
```

- [ ] **Step 2: Implement mapper**

Mapper behavior:

- Trims all user text.
- Defaults empty country to `Germany`.
- Generates long random edit token using `RandomNumberGenerator`.
- Normalizes idCard with `IdCardService.Normalize`.
- Uses `IdCardService.CreateTemporaryId(submissionId, competitorIndex)` when idCard is missing.
- Maps `TeamPayload.Members[].CompetitorClientId` to stored `TeamMember.CompetitorIdCard`.
- `ApplyPayload` keeps existing `Submission.Id` and `EditToken`, updates `UpdatedAtUtc`, sets status to `NeedsReview`, updates club/contact fields in place, and replaces competitors/teams.
- `ToPayload` converts an existing submission back into browser payload, using `IdCard` as `ClientId`.

- [ ] **Step 3: Write CSV tests**

Expected output lines:

```text
#name;country;city;address;email;phone;web
Kissaki Kendo;Germany;Lahr;;info@example.org;;
#Name;Lastname;idCard;Club;ClubCity
Max;Mustermann;A12345;Kissaki Kendo;Lahr
#name;tournament;member1;member2;member3;member4;member5;member6;member7;member8;member9
Kissaki-Team-1;Kissaki Cup 2026;A12345;B67890;C24680;;;;;;
```

- [ ] **Step 4: Implement CSV export service**

Exporter behavior:

- UTF-8 with BOM.
- Semicolon separated.
- Quotes fields containing semicolon, quote, CR, or LF.
- `ExportClubs` deduplicates by normalized club name + city.
- `ExportParticipants` writes first name, last name, idCard, club name, club city.
- `ExportTeams` writes configured tournament name and exactly nine member columns.
- Disabled submissions are skipped.

- [ ] **Step 5: Register service, verify, commit**

```powershell
dotnet test KissakiSignup.sln
git add src/KissakiSignup.Web/Services src/KissakiSignup.Web/Program.cs tests/KissakiSignup.Tests
git commit -m "feat: add submission mapping and csv export"
```

---

### Task 5: Public Registration Wizard

**Files:**
- Modify: `src/KissakiSignup.Web/Pages/Index.cshtml`
- Modify: `src/KissakiSignup.Web/Pages/Index.cshtml.cs`
- Modify: `src/KissakiSignup.Web/Program.cs`
- Create: `src/KissakiSignup.Web/Pages/Confirmation.cshtml`
- Create: `src/KissakiSignup.Web/Pages/Confirmation.cshtml.cs`
- Create: `src/KissakiSignup.Web/wwwroot/js/registration-form.js`
- Modify: `src/KissakiSignup.Web/wwwroot/css/site.css`
- Create: `tests/KissakiSignup.Tests/PublicRegistrationTests.cs`

**Interfaces:**
- Produces public route `/` with POST handler.
- Produces `/Confirmation/{id}`.
- Produces shared static `IndexModel.Validate(RegistrationPayload payload)`.

- [ ] **Step 1: Write integration tests**

Test `GET /` contains `Kissaki Kendo Cup Anmeldung` and does not contain fields named `ParticipationFee`, `PaymentStatus`, or `payment`.

Test `POST /` with a valid `PayloadJson` and real `__RequestVerificationToken` redirects to `/Confirmation/{id}`.

Use this token helper in tests:

```csharp
private static async Task<string> GetAntiforgeryToken(HttpClient client, string path)
{
    var html = await client.GetStringAsync(path);
    var match = Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"");
    match.Success.Should().BeTrue();
    return match.Groups[1].Value;
}
```

- [ ] **Step 2: Implement public page model**

`IndexModel.OnPostAsync()` behavior:

- If hidden form field `website` is non-empty, return `NoContentResult` and save nothing.
- If registration is closed or after deadline, add model error.
- Deserialize `PayloadJson` with case-insensitive JSON options.
- Call `Validate(payload)`.
- Add model errors for blocking rule messages.
- Save `SubmissionMapper.CreateSubmission(payload)`.
- Redirect to confirmation.

`Validate(payload)` checks:

- club name required.
- club city required.
- contact name and email required.
- at least one competitor required.
- competitor categories through `TournamentRules.ValidateCompetitor`.
- team members through `TournamentRules.ValidateTeam`.

- [ ] **Step 3: Implement public wizard markup**

Wizard steps:

1. Club fields.
2. Contact fields.
3. Competitors list with add button.
4. Teams list with add button.
5. Review screen with payment note.

Include hidden `PayloadJson` and hidden honeypot input named `website`. Do not include any fee or payment data input.

- [ ] **Step 4: Implement `registration-form.js`**

JavaScript behavior:

- Maintains `state = { club, contact, competitors, teams }`.
- If `window.initialRegistrationPayload` exists, hydrate state from it.
- Writes `PayloadJson` before submit.
- Adds/removes competitors in browser state.
- Adds teams with position 1-3 member selects.
- Renders category options for `WithoutBogu`, `Age7To9`, `Age10To12`, `Age13To15`, `Age16To18`, `AdultKyu`.
- Hydrates static fields and selected options on edit pages.
- Escapes inserted display strings via `escapeHtml`.

- [ ] **Step 5: Add styling**

Use a quiet, operational UI:

```css
.form-shell { max-width: 960px; margin: 0 auto; padding: 2rem 1rem; }
.wizard-step, .entry, .notice { border: 1px solid #d8d8d8; border-radius: 6px; padding: 1rem; margin: 1rem 0; background: #fff; }
label { display: grid; gap: .35rem; margin: .75rem 0; font-weight: 600; }
input, select, textarea, button { font: inherit; }
input, select, textarea { border: 1px solid #b8b8b8; border-radius: 4px; padding: .55rem .65rem; }
.wizard-nav { display: flex; gap: .75rem; justify-content: flex-end; }
.validation { color: #9b1c1c; }
.honeypot { position: absolute; left: -9999px; }
```

- [ ] **Step 6: Add confirmation page**

Confirmation shows:

- saved confirmation text.
- private edit link `/edit/{token}`.
- payment instruction note: participation fee is paid separately according to the announcement.

- [ ] **Step 7: Add rate limiting**

Use ASP.NET Core rate limiting with fixed window policy `forms`, `PermitLimit = 30`, `Window = TimeSpan.FromMinutes(1)`, `QueueLimit = 0`, and apply it to `MapRazorPages()`.

- [ ] **Step 8: Verify and commit**

```powershell
dotnet test KissakiSignup.sln
git add src/KissakiSignup.Web/Pages src/KissakiSignup.Web/Program.cs src/KissakiSignup.Web/wwwroot tests/KissakiSignup.Tests
git commit -m "feat: add public registration flow"
```

---

### Task 6: Private Edit Link

**Files:**
- Create: `src/KissakiSignup.Web/Pages/Edit.cshtml`
- Create: `src/KissakiSignup.Web/Pages/Edit.cshtml.cs`
- Modify: `src/KissakiSignup.Web/Services/SubmissionMapper.cs`
- Create: `tests/KissakiSignup.Tests/EditRegistrationTests.cs`

**Interfaces:**
- Consumes: `SubmissionMapper.ToPayload` and `SubmissionMapper.ApplyPayload`.
- Produces route `/edit/{token}`.

- [ ] **Step 1: Write edit tests**

Test unknown token returns 404.

Test POST to a seeded valid token with changed club name and real antiforgery token redirects to confirmation.

- [ ] **Step 2: Implement edit page model**

`OnGetAsync(token)`:

- Loads submission with club, contact, competitors, categories, teams, members.
- Returns 404 if token unknown.
- Sets `InitialPayloadJson` to camelCase JSON from `SubmissionMapper.ToPayload(submission)`.

`OnPostAsync(token)`:

- Loads submission.
- Deserializes `PayloadJson`.
- Calls `IndexModel.Validate(payload)`.
- Adds blocking errors.
- On valid payload calls `SubmissionMapper.ApplyPayload` and saves.
- Redirects to confirmation.

- [ ] **Step 3: Implement edit wizard markup**

Use the same wizard structure as the public form. Add:

```cshtml
<script>
window.initialRegistrationPayload = @Html.Raw(Model.InitialPayloadJson);
</script>
```

before `registration-form.js`.

- [ ] **Step 4: Verify and commit**

```powershell
dotnet test KissakiSignup.sln
git add src/KissakiSignup.Web/Pages/Edit.cshtml src/KissakiSignup.Web/Pages/Edit.cshtml.cs src/KissakiSignup.Web/Services/SubmissionMapper.cs tests/KissakiSignup.Tests
git commit -m "feat: add private edit links"
```

---

### Task 7: Admin Authentication And Review Pages

**Files:**
- Modify: `src/KissakiSignup.Web/Program.cs`
- Create: `src/KissakiSignup.Web/Pages/Admin/Login.cshtml`
- Create: `src/KissakiSignup.Web/Pages/Admin/Login.cshtml.cs`
- Create: `src/KissakiSignup.Web/Pages/Admin/Index.cshtml`
- Create: `src/KissakiSignup.Web/Pages/Admin/Index.cshtml.cs`
- Create: `src/KissakiSignup.Web/Pages/Admin/Submission.cshtml`
- Create: `src/KissakiSignup.Web/Pages/Admin/Submission.cshtml.cs`
- Create: `tests/KissakiSignup.Tests/AdminTests.cs`

**Interfaces:**
- Produces password login at `/admin/login`.
- Produces admin list `/admin`.
- Produces admin detail `/admin/submission/{id}`.

- [ ] **Step 1: Write admin tests**

Test anonymous `/admin` redirects to `/admin/login`.

Test correct password plus antiforgery token logs in and admin page contains `Meldungen`.

- [ ] **Step 2: Configure authentication**

Use cookie authentication:

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.LoginPath = "/admin/login";
    options.AccessDeniedPath = "/admin/login";
    options.Cookie.Name = "KissakiSignup.Admin";
    options.SlidingExpiration = true;
});
builder.Services.AddAuthorization();
```

Add `app.UseAuthentication()` before `app.UseAuthorization()`.

- [ ] **Step 3: Implement login**

`LoginModel.OnPostAsync()` compares the submitted password to configured `TournamentOptions.AdminPassword` using SHA256 hashes and `CryptographicOperations.FixedTimeEquals`. Empty configured password always fails.

- [ ] **Step 4: Implement admin list**

List all submissions ordered by `UpdatedAtUtc` descending with club, city, competitor count, status, and detail link.

Show export links:

```html
/admin/export/clubs.csv
/admin/export/participants.csv
/admin/export/teams.csv
```

- [ ] **Step 5: Implement admin detail**

Show club, contact, competitors, teams, private edit link, and status form.

Status values:

```csharp
New, NeedsReview, Reviewed, Exported, Disabled
```

On status change, add an `AdminNote` with timestamp and text `Status geaendert auf {status}.`.

- [ ] **Step 6: Verify and commit**

```powershell
dotnet test KissakiSignup.sln
git add src/KissakiSignup.Web/Program.cs src/KissakiSignup.Web/Pages/Admin tests/KissakiSignup.Tests
git commit -m "feat: add admin review area"
```

---

### Task 8: Admin CSV Download Endpoints

**Files:**
- Create: `src/KissakiSignup.Web/Pages/Admin/Export.cshtml.cs`
- Create: `tests/KissakiSignup.Tests/AdminExportTests.cs`

**Interfaces:**
- Produces `/admin/export/clubs.csv`.
- Produces `/admin/export/participants.csv`.
- Produces `/admin/export/teams.csv`.

- [ ] **Step 1: Write export endpoint tests**

For all three export URLs, anonymous request redirects to `/admin/login`.

- [ ] **Step 2: Implement export model**

`ExportModel.OnGetAsync(string file)` loads all non-disabled submissions with club, competitors, categories, teams, and members.

Switch exact filenames:

```csharp
"clubs.csv" => File(csvExport.ExportClubs(submissions), "text/csv; charset=utf-8", "clubs.csv")
"participants.csv" => File(csvExport.ExportParticipants(submissions), "text/csv; charset=utf-8", "participants.csv")
"teams.csv" => File(csvExport.ExportTeams(submissions), "text/csv; charset=utf-8", "teams.csv")
```

Unknown filename returns 404.

- [ ] **Step 3: Add route convention**

```csharp
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AddPageRoute("/Admin/Export", "/admin/export/{file}");
});
```

- [ ] **Step 4: Verify and commit**

```powershell
dotnet test KissakiSignup.sln
git add src/KissakiSignup.Web/Pages/Admin/Export.cshtml.cs src/KissakiSignup.Web/Program.cs tests/KissakiSignup.Tests
git commit -m "feat: add admin csv exports"
```

---

### Task 9: Windows/IIS Deployment Documentation

**Files:**
- Create: `README.md`
- Create: `docs/deployment/windows-iis.md`

**Interfaces:**
- Produces operation instructions for Henri.

- [ ] **Step 1: Write README**

Include local run commands:

```powershell
dotnet restore KissakiSignup.sln
dotnet ef database update --project src/KissakiSignup.Web/KissakiSignup.Web.csproj --startup-project src/KissakiSignup.Web/KissakiSignup.Web.csproj
dotnet run --project src/KissakiSignup.Web/KissakiSignup.Web.csproj
```

Include configuration keys:

```text
Tournament__AdminPassword
Tournament__DatabasePath
Tournament__TournamentName
Tournament__RegistrationOpen
Tournament__RegistrationDeadline
```

Include export URLs.

- [ ] **Step 2: Write Windows/IIS guide**

Document:

- create small Azure Windows Server VM.
- install IIS Web Server role.
- install .NET Hosting Bundle.
- publish with `dotnet publish src/KissakiSignup.Web/KissakiSignup.Web.csproj -c Release -o artifacts/publish`.
- copy publish folder to `C:\inetpub\KissakiSignup`.
- app pool set to `No Managed Code`.
- app pool identity gets write permission to `C:\inetpub\KissakiSignup\App_Data`.
- set environment variables for tournament config.
- backup `App_Data\kissaki-registration.sqlite` before KTM exports.
- close registration by setting `Tournament__RegistrationOpen=false` and restarting the IIS site.

- [ ] **Step 3: Verify publish and commit**

```powershell
dotnet publish src/KissakiSignup.Web/KissakiSignup.Web.csproj -c Release -o artifacts/publish
git add README.md docs/deployment/windows-iis.md
git commit -m "docs: add windows deployment guide"
```

---

### Task 10: End-To-End Verification And Polish

**Files:**
- Modify: `src/KissakiSignup.Web/Pages/*.cshtml`
- Modify: `src/KissakiSignup.Web/wwwroot/css/site.css`
- Modify: `tests/KissakiSignup.Tests/*`

**Interfaces:**
- Produces final verified v1.

- [ ] **Step 1: Run automated verification**

```powershell
dotnet format KissakiSignup.sln --verify-no-changes
dotnet test KissakiSignup.sln
dotnet publish src/KissakiSignup.Web/KissakiSignup.Web.csproj -c Release -o artifacts/publish
```

Expected: format check, tests, and publish succeed.

- [ ] **Step 2: Run app locally**

```powershell
dotnet run --project src/KissakiSignup.Web/KissakiSignup.Web.csproj
```

Expected: terminal prints localhost URL.

- [ ] **Step 3: Manual public form check**

Verify:

```text
Start page shows "Kissaki Kendo Cup Anmeldung".
No field asks for participation fee or payment status.
Club, contact, competitor, team, review steps can be reached.
Submitting valid sample data redirects to confirmation.
Confirmation shows private edit link and separate payment note.
```

- [ ] **Step 4: Manual admin check**

Verify:

```text
Anonymous user redirects to /admin/login.
Configured password logs in.
Admin list shows submitted club.
Admin detail shows competitors and teams.
Status can be changed to Reviewed and Disabled.
```

- [ ] **Step 5: Manual KTM export check**

Verify headers:

```text
clubs.csv starts with #name;country;city;address;email;phone;web
participants.csv starts with #Name;Lastname;idCard;Club;ClubCity
teams.csv starts with #name;tournament;member1;member2;member3;member4;member5;member6;member7;member8;member9
```

Verify entered `a-123 45` appears as `A12345`.

- [ ] **Step 6: Commit final polish**

```powershell
git status --short
git add src tests docs README.md
git commit -m "chore: verify signup app v1"
```

Expected: final commit created or no changes are present.

---

## Self-Review

- Spec coverage: public no-login form is covered by Task 5; private edit link by Task 6; admin review by Task 7; KTM exports by Tasks 4 and 8; Windows/IIS deployment by Task 9; tests and manual browser checks by Task 10.
- Participation fee exclusion: covered in Global Constraints, Task 5 tests/markup, confirmation note, and Task 10 manual check.
- Technical basis: .NET 10 LTS, Razor Pages, SQLite, IIS, no Docker covered in header, Task 1, and Task 9.
- Type consistency: `RegistrationPayload`, `Submission`, `ApplicationDbContext`, `CsvExportService`, `TournamentOptions`, `RegistrationStatus`, `CompetitionCategory`, and `TeamType` names are introduced before use in later tasks.
- Scope check: this is one cohesive v1 app. Payment handling, direct KTM API integration, and Club-Manager accounts remain outside v1.
