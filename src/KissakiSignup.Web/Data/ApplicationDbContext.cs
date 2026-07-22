using KissakiSignup.Web.Domain;
using Microsoft.EntityFrameworkCore;

namespace KissakiSignup.Web.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Submission> Submissions => Set<Submission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Submission>(entity =>
        {
            entity.HasIndex(submission => submission.EditToken).IsUnique();
            entity.HasOne(submission => submission.Club)
                .WithOne()
                .HasForeignKey<Club>(club => club.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(submission => submission.Contact)
                .WithOne()
                .HasForeignKey<Contact>(contact => contact.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(submission => submission.Competitors)
                .WithOne()
                .HasForeignKey(competitor => competitor.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(submission => submission.Teams)
                .WithOne()
                .HasForeignKey(team => team.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(submission => submission.AdminNotes)
                .WithOne()
                .HasForeignKey(note => note.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Competitor>(entity =>
        {
            entity.HasIndex(competitor => new { competitor.SubmissionId, competitor.IdCard }).IsUnique();
            entity.HasMany(competitor => competitor.Categories)
                .WithOne()
                .HasForeignKey(category => category.CompetitorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Team>(entity =>
        {
            entity.HasMany(team => team.Members)
                .WithOne()
                .HasForeignKey(member => member.TeamId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
