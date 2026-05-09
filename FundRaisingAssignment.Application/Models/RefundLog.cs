using System.ComponentModel.DataAnnotations;

namespace FundRaisingAssignment.Application.Models;

/// <summary>
/// Audit row written every time an admin refunds a donation. One row per
/// refund — captures who did it, when, how much, and why, so refund activity
/// is queryable for reports without parsing free-text notes.
/// </summary>
public class RefundLog
{
    public Guid Id { get; set; }

    public Guid DonationId { get; set; }
    public Donation? Donation { get; set; }

    public Guid CampaignId { get; set; }
    public Campaign? Campaign { get; set; }

    /// <summary>Admin who performed the refund. Nullable so the row survives if the admin account is later removed.</summary>
    public Guid? AdminId { get; set; }
    public ApplicationUser? Admin { get; set; }

    /// <summary>Display label snapshot of the admin (e.g. email at time of refund).</summary>
    [MaxLength(256)]
    public string AdminLabel { get; set; } = "";

    public decimal Amount { get; set; }

    [MaxLength(1000)]
    public string? Reason { get; set; }

    public DateTime RefundedAt { get; set; } = DateTime.UtcNow;
}
