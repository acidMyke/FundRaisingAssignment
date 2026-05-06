using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FundRaisingAssignment.Application.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Campaign>              Campaigns              { get; set; }
    public DbSet<CampaignReview>        CampaignReviews        { get; set; }
    public DbSet<FundRaiserNotification> FundRaiserNotifications { get; set; }
    public DbSet<Donation>              Donations              { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        SetupUtcConverter(builder);

        // Identity tables
        builder.Entity<ApplicationUser>(e => e.ToTable("Users"));
        builder.Entity<ApplicationRole>(e => e.ToTable("Roles"));
        builder.Entity<IdentityUserRole<Guid>>(e => e.ToTable("UserRoles"));
        builder.Entity<IdentityUserClaim<Guid>>(e => e.ToTable("UserClaims"));
        builder.Entity<IdentityUserLogin<Guid>>(e => e.ToTable("UserLogins"));
        builder.Entity<IdentityRoleClaim<Guid>>(e => e.ToTable("RoleClaims"));
        builder.Entity<IdentityUserToken<Guid>>(e => e.ToTable("UserTokens"));

        // Campaign → Owner
        builder.Entity<Campaign>(e =>
        {
            e.HasOne(c => c.Owner)
             .WithMany()
             .HasForeignKey(c => c.OwnerId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // CampaignReview → Campaign + Reviewer, unique per user
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

        // FundRaiserNotification → Campaign
        builder.Entity<FundRaiserNotification>(e =>
        {
            e.HasKey(n => n.NotificationId);
            e.HasOne(n => n.Campaign)
             .WithMany()
             .HasForeignKey(n => n.CampaignId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Donation → Campaign (cascade) and optional Donor
        builder.Entity<Donation>(e =>
        {
            e.HasKey(d => d.DonationId);
            e.HasOne(d => d.Campaign)
             .WithMany()
             .HasForeignKey(d => d.CampaignId)
             .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(d => d.Donor)
             .WithMany()
             .HasForeignKey(d => d.DonorId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
            // Enforce positive amount at DB level
            e.ToTable("Donations", t =>
                t.HasCheckConstraint("CK_Donations_Amount_Positive", "\"Amount\" > 0"));
        });
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
