# Kissaki Signup

Kissaki Signup is an ASP.NET Core Razor Pages application for collecting tournament registrations and exporting data for KendoTournamentManager (KTM).

## Local development

Run these commands from the repository root:

```powershell
dotnet restore KissakiSignup.sln
dotnet ef database update --project src/KissakiSignup.Web/KissakiSignup.Web.csproj --startup-project src/KissakiSignup.Web/KissakiSignup.Web.csproj
dotnet run --project src/KissakiSignup.Web/KissakiSignup.Web.csproj
```

The public registration page is available at `/`. The admin area starts at `/admin` and requires the configured admin password.

## Configuration

Configuration uses the `Tournament` section in `appsettings.json`. For environment variables, use the double-underscore form:

```text
Tournament__AdminPassword
Tournament__DatabasePath
Tournament__TournamentName
Tournament__RegistrationOpen
Tournament__RegistrationDeadline
```

Do not commit production passwords or production database paths to source control.

## Export URLs

After signing in to the admin area, use these URLs to download the KTM exports:

- `/admin/export/clubs.csv`
- `/admin/export/participants.csv`
- `/admin/export/teams.csv`

See [Windows/IIS deployment](docs/deployment/windows-iis.md) for production deployment and backup instructions.
