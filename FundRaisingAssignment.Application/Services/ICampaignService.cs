using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Services;

public interface ICampaignService
{
    // ── Campaign CRUD ──────────────────────────────────────────────────────────
    /// <summary>Create a new campaign (starts as Draft).</summary>
    Task<Campaign> CreateCampaignAsync(Campaign campaign);

    /// <summary>Update campaign details – admin only after initial submission.</summary>
    Task<Campaign> UpdateCampaignDetailsAsync(Campaign campaign);

    /// <summary>BCE D1 – create quick campaign with goal + deadline only (legacy).</summary>
    Task<Campaign> SaveGoalAndDeadlineAsync(decimal goalAmount, DateTime deadlineDate, Guid ownerId);

    /// <summary>BCE D1 – update goal + deadline on existing campaign (fundraiser always allowed).</summary>
    Task<Campaign> UpdateGoalAndDeadlineAsync(Guid campaignId, decimal goalAmount, DateTime deadlineDate, Guid ownerId);

    // ── Fundraiser workflow ────────────────────────────────────────────────────
    /// <summary>Fundraiser submits a Draft campaign for platform management review.</summary>
    Task<Campaign> SubmitForReviewAsync(Guid campaignId, Guid ownerId);

    // ── Platform Management (Admin) workflow ───────────────────────────────────
    /// <summary>Admin publishes a PendingReview campaign → Active (visible to donors).</summary>
    Task<Campaign> PublishCampaignAsync(Guid campaignId);

    /// <summary>Admin flags an Active campaign for later review (removed from public).</summary>
    Task<Campaign> FlagCampaignByAdminAsync(Guid campaignId, string reason);

    /// <summary>Admin pauses a campaign (removed from public, no donations).</summary>
    Task<Campaign> PauseCampaignAsync(Guid campaignId, string reason);

    /// <summary>Admin terminates a campaign permanently.</summary>
    Task<Campaign> TerminateCampaignAsync(Guid campaignId, string reason);

    /// <summary>Admin releases a Flagged or Paused campaign back to Active.</summary>
    Task<Campaign> ReleaseCampaignAsync(Guid campaignId);

    // ── Queries ────────────────────────────────────────────────────────────────
    Task<Campaign?> GetCampaignAsync(Guid campaignId);
    Task<Campaign?> GetCampaignDetailsAsync(Guid campaignId);
    Task<IReadOnlyList<Campaign>> GetCampaignsByOwnerAsync(Guid ownerId);
    Task<IReadOnlyList<Campaign>> GetAllCampaignsAsync();

    /// <summary>Returns only Active campaigns – shown on the public Campaigns page.</summary>
    Task<IReadOnlyList<Campaign>> GetPublicCampaignsAsync();

    /// <summary>Returns campaigns awaiting admin publish decision.</summary>
    Task<IReadOnlyList<Campaign>> GetPendingReviewCampaignsAsync();

    /// <summary>Returns campaigns currently flagged (auto or by admin).</summary>
    Task<IReadOnlyList<Campaign>> GetFlaggedCampaignsAsync();

    // ── BCE Diagram 2 – flag decision ─────────────────────────────────────────
    Task<Campaign> ApproveCampaignAsync(Guid campaignId);
    Task<Campaign> RemoveCampaignAsync(Guid campaignId, string removalReason);
    Task<Campaign> FlagCampaignAsync(Guid campaignId, string flagReason);
    Task<FundRaiserNotification> NotifyFundRaiserAsync(Guid campaignId, string reviewOutcome);

    // ── Donor reviews ──────────────────────────────────────────────────────────
    Task<CampaignReview> AddReviewAsync(Guid campaignId, Guid reviewerId,
                                        string reviewerEmail, int stars, string? comment);
    Task<IReadOnlyList<CampaignReview>> GetCampaignReviewsAsync(Guid campaignId);
    Task<bool> HasUserReviewedAsync(Guid campaignId, Guid userId);

    // ── Donations ──────────────────────────────────────────────────────────────
    Task<Donation> DonateAsync(Guid campaignId, Guid? donorId, string donorEmail,
                               decimal amount, string? message, bool isAnonymous);
    Task<IReadOnlyList<Donation>> GetCampaignDonationsAsync(Guid campaignId);
    Task<IReadOnlyList<Donation>> GetDonationsByUserAsync(Guid userId);
    Task<decimal> GetTotalDonatedAsync(Guid campaignId);
}
