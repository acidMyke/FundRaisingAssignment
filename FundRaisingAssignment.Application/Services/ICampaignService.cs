using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Services;

public interface ICampaignService
{
    // ── Campaign CRUD ──────────────────────────────────────────────────────────
    Task<Campaign> CreateCampaignAsync(Campaign campaign);
    Task<Campaign> UpdateCampaignDetailsAsync(Campaign campaign);
    Task<Campaign> SaveGoalAndDeadlineAsync(decimal goalAmount, DateTime deadlineDate, Guid ownerId);
    Task<Campaign> UpdateGoalAndDeadlineAsync(Guid campaignId, decimal goalAmount, DateTime deadlineDate, Guid ownerId);

    // ── Search (Karthik) ───────────────────────────────────────────────────────
    Task<IReadOnlyList<Campaign>> SearchCampaignsAsync(string? keyword, string? category, string? location);

    // ── Fundraiser workflow ────────────────────────────────────────────────────
    Task<Campaign> SubmitForReviewAsync(Guid campaignId, Guid ownerId);

    // ── Admin workflow ─────────────────────────────────────────────────────────
    Task<Campaign> PublishCampaignAsync(Guid campaignId);
    Task<Campaign> FlagCampaignByAdminAsync(Guid campaignId, string reason);
    Task<Campaign> PauseCampaignAsync(Guid campaignId, string reason);
    Task<Campaign> TerminateCampaignAsync(Guid campaignId, string reason);
    Task<Campaign> ReleaseCampaignAsync(Guid campaignId);

    // ── Queries ────────────────────────────────────────────────────────────────
    Task<Campaign?> GetCampaignAsync(Guid campaignId);
    Task<Campaign?> GetCampaignDetailsAsync(Guid campaignId);
    Task<IReadOnlyList<Campaign>> GetCampaignsByOwnerAsync(Guid ownerId);
    Task<IReadOnlyList<Campaign>> GetAllCampaignsAsync();
    Task<IReadOnlyList<Campaign>> GetPublicCampaignsAsync();
    Task<IReadOnlyList<Campaign>> GetPendingReviewCampaignsAsync();
    Task<IReadOnlyList<Campaign>> GetFlaggedCampaignsAsync();

    // ── BCE Diagram 2 ──────────────────────────────────────────────────────────
    Task<Campaign> ApproveCampaignAsync(Guid campaignId);
    Task<Campaign> RemoveCampaignAsync(Guid campaignId, string removalReason);
    Task<Campaign> FlagCampaignAsync(Guid campaignId, string flagReason);
    Task<FundRaiserNotification> NotifyFundRaiserAsync(Guid campaignId, string reviewOutcome);

    // ── Reviews ────────────────────────────────────────────────────────────────
    Task<CampaignReview> AddReviewAsync(Guid campaignId, Guid reviewerId,
                                        string reviewerEmail, int stars, string? comment);
    Task<IReadOnlyList<CampaignReview>> GetCampaignReviewsAsync(Guid campaignId);
    Task<bool> HasUserReviewedAsync(Guid campaignId, Guid userId);

    // ── Donation queries (writes go through DonationService) ──────────────────
    Task<IReadOnlyList<Donation>> GetCampaignDonationsAsync(Guid campaignId);
    Task<IReadOnlyList<Donation>> GetDonationsByUserAsync(Guid userId);
    Task<decimal> GetTotalDonatedAsync(Guid campaignId);
}
