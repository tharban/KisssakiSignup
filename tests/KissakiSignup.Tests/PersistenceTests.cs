using FluentAssertions;
using KissakiSignup.Web.Data;
using KissakiSignup.Web.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace KissakiSignup.Tests;

public class PersistenceTests
{
    [Fact]
    public async Task MigrateAsync_MapsLegacyDraftAndSubmittedStatusesToNew()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new ApplicationDbContext(options);
        await context.Database.MigrateAsync("20260722165638_InitialCreate");
        context.Submissions.AddRange(
            CreateLegacySubmission("legacy-draft", 1),
            CreateLegacySubmission("legacy-submitted", 2));
        await context.SaveChangesAsync();

        await context.Database.MigrateAsync();
        context.ChangeTracker.Clear();

        var statuses = await context.Submissions
            .OrderBy(submission => submission.EditToken)
            .Select(submission => submission.Status)
            .ToListAsync();

        statuses.Should().Equal(RegistrationStatus.New, RegistrationStatus.New);
    }

    [Fact]
    public async Task SaveChanges_PersistsCompleteSubmissionGraph()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;

        var submissionId = Guid.NewGuid();
        await using (var context = new ApplicationDbContext(options))
        {
            context.Submissions.Add(new Submission
            {
                Id = submissionId,
                EditToken = "edit-token",
                Status = RegistrationStatus.New,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                Club = new Club { Name = "Kissaki Kendo" },
                Contact = new Contact { Name = "Erika Beispiel" },
                Competitors =
                [
                    new Competitor
                    {
                        FirstName = "Max",
                        LastName = "Mustermann",
                        IdCard = "A12345",
                        Categories = [new CompetitorCategory { Category = CompetitionCategory.Age10To12 }]
                    }
                ],
                Teams =
                [
                    new Team
                    {
                        Name = "Kissaki Team",
                        TeamType = TeamType.Youth,
                        Members = [new TeamMember { Position = 1, CompetitorIdCard = "A12345" }]
                    }
                ]
            });

            await context.Database.EnsureCreatedAsync();
            await context.SaveChangesAsync();
        }

        await using var verificationContext = new ApplicationDbContext(options);
        var saved = await verificationContext.Submissions
            .Include(submission => submission.Club)
            .Include(submission => submission.Contact)
            .Include(submission => submission.Competitors)
            .ThenInclude(competitor => competitor.Categories)
            .Include(submission => submission.Teams)
            .ThenInclude(team => team.Members)
            .SingleAsync(submission => submission.Id == submissionId);

        saved.Club.Name.Should().Be("Kissaki Kendo");
        saved.Competitors.Single().Categories.Single().Category.Should().Be(CompetitionCategory.Age10To12);
        saved.Teams.Single().Members.Single().CompetitorIdCard.Should().Be("A12345");
    }

    private static Submission CreateLegacySubmission(string editToken, int status) => new()
    {
        Id = Guid.NewGuid(),
        EditToken = editToken,
        Status = (RegistrationStatus)status,
        CreatedAtUtc = DateTimeOffset.UtcNow,
        UpdatedAtUtc = DateTimeOffset.UtcNow
    };
}
