using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FundRaisingAssignment.Application.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<Campaign> Campaigns { get; set; }
    public DbSet<DonationRecord> DonationRecords { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        SetupUtcConverter(builder);

        builder.Entity<ApplicationUser>(entity => { entity.ToTable("Users"); });
        builder.Entity<ApplicationRole>(entity => { entity.ToTable("Roles"); });
        builder.Entity<IdentityUserRole<Guid>>(entity => { entity.ToTable("UserRoles"); });
        builder.Entity<IdentityUserClaim<Guid>>(entity => { entity.ToTable("UserClaims"); });
        builder.Entity<IdentityUserLogin<Guid>>(entity => { entity.ToTable("UserLogins"); });
        builder.Entity<IdentityRoleClaim<Guid>>(entity => { entity.ToTable("RoleClaims"); });
        builder.Entity<IdentityUserToken<Guid>>(entity => { entity.ToTable("UserTokens"); });

    }

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
