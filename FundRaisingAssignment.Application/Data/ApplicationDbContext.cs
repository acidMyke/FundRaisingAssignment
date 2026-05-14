using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   Cross-cutting (persistence layer)           Owner: Team
// BCE Role:     Entity / persistence
// Description:  Single EF Core DbContext. Hosts every story's DbSet, the
//               Identity overrides, FK + index configuration, and the
//               SGT-aware DateTime value converter.
// Notes:        Per-entity Format-B regions inside OnModelCreating identify
//               which user story owns each table's configuration.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Application.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Campaign> Campaigns { get; set; }                 // FR01, PM01, DN01
    public DbSet<Donee> Donees { get; set; }                       // DN02 — Karthik
    public DbSet<Donation> Donations { get; set; }                 // DN03 — Shared
    public DbSet<DonationGoal> DonationGoals { get; set; }         // DN02 — Karthik
    public DbSet<ExportFile> ExportFiles { get; set; }             // UA02 — Karthik
    public DbSet<RefundLog> RefundLogs { get; set; }               // DN03 — Shared (refund pipeline)


    public DbSet<CampaignReview> CampaignReviews { get; set; }     // PM01 — Josh
    public DbSet<FundRaiserNotification> FundRaiserNotifications { get; set; } // PM01 — Josh

    // ── Badge Metrics ─────────────────────────────────────────────
    public DbSet<UserMetrics> UserMetrics { get; set; }

    public DbSet<CampaignVisit> CampaignVisits { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        SetupUtcConverter(builder);

        // ── Identity table renames ─────────────────────────────────────────────
        builder.Entity<ApplicationUser>(e => e.ToTable("Users"));
        builder.Entity<ApplicationRole>(e => e.ToTable("Roles"));
        builder.Entity<IdentityUserRole<Guid>>(e => e.ToTable("UserRoles"));
        builder.Entity<IdentityUserClaim<Guid>>(e => e.ToTable("UserClaims"));
        builder.Entity<IdentityUserLogin<Guid>>(e => e.ToTable("UserLogins"));
        builder.Entity<IdentityRoleClaim<Guid>>(e => e.ToTable("RoleClaims"));
        builder.Entity<IdentityUserToken<Guid>>(e => e.ToTable("UserTokens"));

        #region Campaign (FR01, PM01)
        builder.Entity<Campaign>(b =>
        {
            b.HasOne(c => c.Owner)
             .WithMany()
             .HasForeignKey(c => c.OwnerId)
             .OnDelete(DeleteBehavior.Cascade);
        });
        #endregion

        #region Donation (DN03)
        builder.Entity<Donation>(b =>
        {
            b.HasOne(d => d.Campaign)
             .WithMany()
             .HasForeignKey(d => d.CampaignId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(d => d.User)
             .WithMany()
             .HasForeignKey(d => d.UserId)
             .IsRequired(false)            // guest donations supported (Josh's flow)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasIndex(d => d.CampaignId);
            b.HasIndex(d => d.UserId);
            b.HasIndex(d => d.CreatedAt);
            b.HasIndex(d => new { d.UserId, d.CreatedAt });
        });
        #endregion

        #region DonationGoal (DN02)
        builder.Entity<DonationGoal>(b =>
        {
            b.HasIndex(g => g.UserId).IsUnique();
            b.HasOne(g => g.User)
             .WithMany()
             .HasForeignKey(g => g.UserId)
             .OnDelete(DeleteBehavior.Cascade);
        });
        #endregion

        #region Donee (DN02)
        builder.Entity<Donee>(e =>
        {
            e.ToTable("Donees");
            e.HasKey(d => d.DoneeID);
            e.Property(d => d.Name).IsRequired().HasMaxLength(100);
            e.Property(d => d.Email).IsRequired();
            e.HasOne(d => d.User)
             .WithMany()
             .HasForeignKey(d => d.UserId)
             .OnDelete(DeleteBehavior.SetNull);
        });
        #endregion

        #region ExportFile (UA02)
        builder.Entity<ExportFile>(b =>
        {
            b.HasOne(e => e.CreatedByAdmin)
             .WithMany()
             .HasForeignKey(e => e.CreatedByAdminId)
             .OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(e => e.CreatedByAdminId);
            b.HasIndex(e => e.CreatedAt);
        });
        #endregion

        #region RefundLog (DN03 — refund pipeline)
        builder.Entity<RefundLog>(b =>
        {
            b.HasOne(r => r.Donation)
             .WithMany()
             .HasForeignKey(r => r.DonationId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(r => r.Campaign)
             .WithMany()
             .HasForeignKey(r => r.CampaignId)
             .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(r => r.Admin)
             .WithMany()
             .HasForeignKey(r => r.AdminId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);

            b.Property(r => r.Amount).HasColumnType("numeric(18,2)");

            b.HasIndex(r => r.DonationId);
            b.HasIndex(r => r.CampaignId);
            b.HasIndex(r => r.AdminId);
            b.HasIndex(r => r.RefundedAt);
        });
        #endregion

        #region CampaignReview (PM01)
        builder.Entity<CampaignReview>(e =>
        {
            e.HasKey(r => r.ReviewId);
            e.HasOne(r => r.Campaign)
             .WithMany()
             .HasForeignKey(r => r.CampaignId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(r => r.Reviewer)
             .WithMany()
             .HasForeignKey(r => r.ReviewerId)
             .OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(r => new { r.CampaignId, r.ReviewerId }).IsUnique();
        });
        #endregion

        #region FundRaiserNotification (PM01)
        builder.Entity<FundRaiserNotification>(e =>
        {
            e.HasKey(n => n.NotificationId);
            e.HasOne(n => n.Campaign)
             .WithMany()
             .HasForeignKey(n => n.CampaignId)
             .OnDelete(DeleteBehavior.Cascade);
        });
        #endregion
    }

    private static void SetupUtcConverter(ModelBuilder builder)
    {
        var sgtZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        var dtConverter = new ValueConverter<DateTime, DateTime>(
            v => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(v, DateTimeKind.Unspecified), sgtZone),
            v => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(v, sgtZone), DateTimeKind.Local));
        var dtoConverter = new ValueConverter<DateTimeOffset, DateTimeOffset>(
            v => v.ToUniversalTime(),
            v => v.ToOffset(sgtZone.BaseUtcOffset));

        foreach (var entity in builder.Model.GetEntityTypes())
            foreach (var prop in entity.GetProperties())
            {
                if (prop.ClrType == typeof(DateTime) || prop.ClrType == typeof(DateTime?))
                    prop.SetValueConverter(dtConverter);
                if (prop.ClrType == typeof(DateTimeOffset) || prop.ClrType == typeof(DateTimeOffset?))
                    prop.SetValueConverter(dtoConverter);
            }
    }
}
