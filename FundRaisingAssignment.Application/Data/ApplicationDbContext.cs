using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FundRaisingAssignment.Application.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    // -----------------------------
    // DbSets
    // -----------------------------
    public DbSet<Campaign> Campaigns { get; set; }

    // ✅ ADD THIS (Donee table)
    public DbSet<Donee> Donees { get; set; }
    public DbSet<Donation> Donations { get; set; }
    public DbSet<DonationGoal> DonationGoals { get; set; }
    public DbSet<ExportFile> ExportFiles { get; set; }

    // -----------------------------
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        SetupUtcConverter(builder);

        // -----------------------------
        // Identity Table Renaming
        // -----------------------------
        builder.Entity<ApplicationUser>(entity => { entity.ToTable("Users"); });
        builder.Entity<ApplicationRole>(entity => { entity.ToTable("Roles"); });
        builder.Entity<IdentityUserRole<Guid>>(entity => { entity.ToTable("UserRoles"); });
        builder.Entity<IdentityUserClaim<Guid>>(entity => { entity.ToTable("UserClaims"); });
        builder.Entity<IdentityUserLogin<Guid>>(entity => { entity.ToTable("UserLogins"); });
        builder.Entity<IdentityRoleClaim<Guid>>(entity => { entity.ToTable("RoleClaims"); });
        builder.Entity<IdentityUserToken<Guid>>(entity => { entity.ToTable("UserTokens"); });

        // DonationGoal: One row per user (Donee)
        builder.Entity<DonationGoal>(b =>
        {
            b.HasIndex(g => g.UserId).IsUnique();

            b.HasOne(g => g.User)
             .WithMany()
             .HasForeignKey(g => g.UserId)
             .OnDelete(DeleteBehavior.Cascade); // Delete user -> delete their goal
        });

        // Donation: Foreign keys and performance indexes
        builder.Entity<Donation>(b =>
        {
            // Relationship with Campaign
            b.HasOne(d => d.Campaign)
             .WithMany()
             .HasForeignKey(d => d.CampaignId)
             .OnDelete(DeleteBehavior.Restrict); // Restricted to prevent accidental data loss

            // Relationship with Donor (ApplicationUser)
            b.HasOne(d => d.User)
             .WithMany()
             .HasForeignKey(d => d.UserId)
             .OnDelete(DeleteBehavior.Restrict);

            // Performance Indexes
            b.HasIndex(d => d.CampaignId);
            b.HasIndex(d => d.UserId);
            b.HasIndex(d => d.CreatedAt);

            // Composite index for reporting: Who donated and when?
            b.HasIndex(d => new { d.UserId, d.CreatedAt });
        });

        // -----------------------------
        // ✅ Donee Configuration
        // -----------------------------
        builder.Entity<Donee>(entity =>
        {
            entity.ToTable("Donees");

            entity.HasKey(d => d.DoneeID);

            entity.Property(d => d.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(d => d.Email)
                .IsRequired();

            // Optional: link Donee to Identity User
            entity.HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // ExportFile: persisted record of each report export
        builder.Entity<ExportFile>(b =>
        {
            b.HasOne(e => e.CreatedByAdmin)
             .WithMany()
             .HasForeignKey(e => e.CreatedByAdminId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(e => e.CreatedByAdminId);
            b.HasIndex(e => e.CreatedAt);
        });
    }

    // -----------------------------
    // Timezone Handling (SGT <-> UTC)
    // -----------------------------
    private static void SetupUtcConverter(ModelBuilder builder)
    {
        var sgtZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");

        var dateTimeConverter = new ValueConverter<DateTime, DateTime>(
            v => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(v, DateTimeKind.Unspecified), sgtZone),
            v => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(v, sgtZone), DateTimeKind.Local)
        );

        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, DateTimeOffset>(
            v => v.ToUniversalTime(),
            v => v.ToOffset(sgtZone.BaseUtcOffset)
        );

        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime) || property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(dateTimeConverter);

                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(dateTimeOffsetConverter);
            }
        }
    }
}