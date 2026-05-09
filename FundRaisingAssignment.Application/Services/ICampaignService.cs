using FundRaisingAssignment.Application.Models;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   DN03 – Make a Donation to a Campaign        Owner: Shared
// User Story:   FR01 – Set Funding Goal and Deadline        Owner: Zhu Jianshan (Josh)
// User Story:   PM01 – Review Flagged Campaign              Owner: Zhu Jianshan (Josh)
// User Story:   DN01 – Search Fundraising Campaigns         Owner: Khoo Si Kai
// BCE Role:     Control (interface)
// Description:  Application-service contract for campaign lifecycle, search,
//               donations, refunds, and reviews. Sole control surface for the
//               Razor pages and the Web API controller.
// Notes:        DN03's DonateAsync was consolidated from four duplicate
//               implementations; see git history and the Final Report
//               § "Donation flow consolidation".
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Application.Services;

public interface ICampaignService
{
    #region FR01 – Campaign CRUD (Josh)
    Task<Campaign> CreateCampaignAsync(Campaign campaign);
    Task<Campaign> UpdateCampaignDetailsAsync(Campaign campaign);
    Task<Campaign> SaveGoalAndDeadlineAsync(decimal goalAmount, DateTime deadlineDate, Guid ownerId);
    Task<Campaign> UpdateGoalAndDeadlineAsync(Guid campaignId, decimal goalAmount, DateTime deadlineDate, Guid ownerId);
    #endregion

    #region DN01 – Search (Khoo Si Kai)
    Task<IReadOnlyList<Campaign>> SearchCampaignsAsync(string? keyword, string? category, string? location);
    #endregion

    #region FR01 – Fundraiser workflow (Josh)
    Task<Campaign> SubmitForReviewAsync(Guid campaignId, Guid ownerId);
    #endregion

    #region PM01 – Admin lifecycle workflow (Josh)
    Task<Campaign> PublishCampaignAsync(Guid campaignId);
    Task<Campaign> FlagCampaignByAdminAsync(Guid campaignId, string reason);
    Task<Campaign> PauseCampaignAsync(Guid campaignId, string reason);
    Task<Campaign> TerminateCampaignAsync(Guid campaignId, string reason);
    Task<Campaign> ReleaseCampaignAsync(Guid campaignId);
    #endregion

    #region Shared queries
    Task<Campaign?> GetCampaignAsync(Guid campaignId);
    Task<Campaign?> GetCampaignDetailsAsync(Guid campaignId);
    Task<IReadOnlyList<Campaign>> GetCampaignsByOwnerAsync(Guid ownerId);
    Task<IReadOnlyList<Campaign>> GetAllCampaignsAsync();
    Task<IReadOnlyList<Campaign>> GetPublicCampaignsAsync();
    Task<IReadOnlyList<Campaign>> GetPendingReviewCampaignsAsync();
    Task<IReadOnlyList<Campaign>> GetFlaggedCampaignsAsync();
    #endregion

    #region PM01 – BCE Diagram 2 outcomes (Josh)
    Task<Campaign> ApproveCampaignAsync(Guid campaignId);
    Task<Campaign> RemoveCampaignAsync(Guid campaignId, string removalReason);
    Task<Campaign> FlagCampaignAsync(Guid campaignId, string flagReason);
    Task<FundRaiserNotification> NotifyFundRaiserAsync(Guid campaignId, string reviewOutcome);
    #endregion

    #region PM01 – Reviews (Josh)
    Task<CampaignReview> AddReviewAsync(Guid campaignId, Guid reviewerId,
                                        string reviewerEmail, int stars, string? comment);
    Task<IReadOnlyList<CampaignReview>> GetCampaignReviewsAsync(Guid campaignId);
    Task<bool> HasUserReviewedAsync(Guid campaignId, Guid userId);
    #endregion

    #region DN03 – Donations (Shared; consolidated)
    /// <summary>
    /// Canonical donation entry point. All four boundary entry points
    /// (Campaigns/Details, Campaigns/CampaignPage, Donations/Create,
    /// DonationsController) funnel through this method.
    /// </summary>
    /// <remarks>
    /// User Story: DN03 — Make a Donation to a Campaign.
    /// Owner: Shared (consolidated from prior Josh + Karthik duplicates).
    /// </remarks>
    Task<DonationResult> DonateAsync(MakeDonationInput input, CancellationToken ct = default);

    Task<RefundResult> RefundDonationAsync(Guid donationId, Guid? adminId, string adminLabel,
                                           string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<Donation>> GetCampaignDonationsAsync(Guid campaignId);
    Task<IReadOnlyList<Donation>> GetDonationsByUserAsync(Guid userId);
    Task<decimal> GetTotalDonatedAsync(Guid campaignId);
    #endregion

    #region PM06 – Leaderboard (Ho Dan Ze; partial — surfaced inline on CampaignPage)
    Task<IReadOnlyList<Donation>> GetTopDonationsAsync(Guid campaignId, int topCount);
    #endregion
}
