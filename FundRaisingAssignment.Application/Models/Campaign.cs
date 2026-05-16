using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   FR01 – Set Funding Goal and Deadline        Owner: Zhu Jianshan (Josh)
// User Story:   PM01 – Review Flagged Campaign              Owner: Zhu Jianshan (Josh)
// BCE Role:     Entity
// Description:  Core Campaign aggregate. Holds funding goal, deadline,
//               status, ownership, ratings, and exposes the lifecycle
//               transitions invoked by the Manage / Review boundaries.
// Notes:        Lifecycle methods (FR01 status flow, PM01 admin actions) are
//               grouped under Format-B regions below. Location and Category
//               fields belong to DN01 (Si Kai – search) and are read-only
//               from this entity's perspective.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Application.Models
{
    [Table("Campaigns")]
    public class Campaign
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "Campaign Title")]
        public string Title { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "Short Description")]
        public string? ShortDescription { get; set; }

        // ── Category: enum (Josh) ──────────────────────────────────────────────
        [Required]
        [Display(Name = "Category")]
        public CampaignCategory Category { get; set; } = CampaignCategory.Other;

        // ── Location: string (Karthik – used in search) ───────────────────────
        [StringLength(100)]
        [Display(Name = "Location")]
        public string? Location { get; set; }

        [StringLength(500)]
        [Display(Name = "Cover Image URL")]
        public string? CoverImageUrl { get; set; }

        // ── Financial ─────────────────────────────────────────────────────────
        [Required]
        [Range(1, double.MaxValue)]
        [Display(Name = "Funding Goal")]
        public decimal FundingGoal { get; set; }

        [Required]
        [Range(1, double.MaxValue)]
        [Display(Name = "Target Amount")]
        public decimal TargetAmount { get; set; }   // kept for Karthik compat

        public decimal CurrentAmount { get; set; } = 0;

        // ── Dates ─────────────────────────────────────────────────────────────
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime StartDate { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
        public DateTime? EndDate { get; set; }   // nullable for Karthik DB compat

        // ── Status ────────────────────────────────────────────────────────────
        public DateTime? LastDigestSent { get; set; }

        public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

        [StringLength(500)]
        public string? FlagReason { get; set; }

        // ── Rating aggregates (Josh) ──────────────────────────────────────────
        public double AverageRating { get; set; } = 0;
        public int ReviewCount { get; set; } = 0;

        // ── Ownership ─────────────────────────────────────────────────────────
        [Required]
        public Guid OwnerId { get; set; }
        public ApplicationUser? Owner { get; set; }

        // ── Derived helpers ───────────────────────────────────────────────────
        public bool IsPubliclyVisible => Status == CampaignStatus.Active;
        public bool AcceptsDonations => Status == CampaignStatus.Active;

        // ── Domain methods ────────────────────────────────────────────────────

        #region FR01 – Set Funding Goal and Deadline (Josh)
        /// <summary>
        /// Fundraiser submits a Draft campaign for admin review.
        /// </summary>
        /// <remarks>
        /// User Story: FR01 — Set Funding Goal and Deadline.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void SubmitForReview()
        {
            if (Status != CampaignStatus.Draft)
                throw new InvalidOperationException("Only Draft campaigns can be submitted for review.");
            Status = CampaignStatus.PendingReview;
        }

        /// <summary>BCE Diagram 1 – updates goal + deadline.</summary>
        /// <remarks>
        /// User Story: FR01 — Set Funding Goal and Deadline.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void UpdateGoalAndDeadline(decimal goalAmount, DateTime deadlineDate)
        {
            FundingGoal = goalAmount;
            TargetAmount = goalAmount;
            EndDate = deadlineDate;
        }
        #endregion

        #region PM01 – Review Flagged Campaign (Josh)
        /// <summary>
        /// Admin publishes a campaign that is currently PendingReview.
        /// </summary>
        /// <remarks>
        /// User Story: PM01 — Review Flagged Campaign.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void PublishCampaign()
        {
            if (Status != CampaignStatus.PendingReview)
                throw new InvalidOperationException("Only campaigns pending review can be published.");
            Status = CampaignStatus.Active;
            PublishedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Admin flags a campaign and removes it from public listings.
        /// </summary>
        /// <remarks>
        /// User Story: PM01 — Review Flagged Campaign.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void FlagCampaignByAdmin(string reason)
        {
            Status = CampaignStatus.Flagged;
            FlagReason = reason;
        }

        /// <summary>
        /// Admin pauses an Active campaign — donations halted, fundraiser
        /// notified, can be released back to Active.
        /// </summary>
        /// <remarks>
        /// User Story: PM01 — Review Flagged Campaign.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void PauseCampaign(string reason)
        {
            Status = CampaignStatus.Paused;
            FlagReason = reason;
        }

        /// <summary>
        /// Admin permanently terminates a campaign (Cancelled). No further
        /// transitions are allowed.
        /// </summary>
        /// <remarks>
        /// User Story: PM01 — Review Flagged Campaign.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void TerminateCampaign(string reason)
        {
            Status = CampaignStatus.Cancelled;
            FlagReason = reason;
        }

        /// <summary>
        /// Admin releases a Flagged or Paused campaign back to Active.
        /// </summary>
        /// <remarks>
        /// User Story: PM01 — Review Flagged Campaign.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void ReleaseCampaign()
        {
            if (Status != CampaignStatus.Flagged && Status != CampaignStatus.Paused)
                throw new InvalidOperationException("Only Flagged or Paused campaigns can be released.");
            Status = CampaignStatus.Active;
            FlagReason = null;
        }

        /// <summary>BCE Diagram 2 – approve outcome from review.</summary>
        /// <remarks>
        /// User Story: PM01 — Review Flagged Campaign.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void ApproveCampaignStatus()
        {
            Status = CampaignStatus.Active;
            FlagReason = null;
        }

        /// <summary>BCE Diagram 2 – remove (cancel) outcome from review.</summary>
        /// <remarks>
        /// User Story: PM01 — Review Flagged Campaign.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void RemoveCampaignStatus(string removalReason)
        {
            Status = CampaignStatus.Cancelled;
            FlagReason = removalReason;
        }

        /// <summary>
        /// Recomputes the cached rating aggregates after a new review is
        /// added (auto-flag pipeline).
        /// </summary>
        /// <remarks>
        /// User Story: PM01 — Review Flagged Campaign.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void RecalculateRating(double newAverage, int newCount)
        {
            AverageRating = Math.Round(newAverage, 2);
            ReviewCount = newCount;
        }

        /// <summary>
        /// Auto-flag rule: any 1- or 2-star review on an Active campaign moves
        /// it into Flagged with a synthetic reason.
        /// </summary>
        /// <remarks>
        /// User Story: PM01 — Review Flagged Campaign.
        /// Owner: Zhu Jianshan (Josh).
        /// </remarks>
        public void FlagFromLowReview(int stars, string reviewerEmail)
        {
            if (Status == CampaignStatus.Active && stars <= 2)
            {
                Status = CampaignStatus.Flagged;
                FlagReason = $"Auto-flagged: low review ({stars} stars) from {reviewerEmail}.";
            }
        }


        public decimal GetProgressPercentage()
        {
            if (FundingGoal <= 0) return 0;
            return Math.Min(100, Math.Round(CurrentAmount / FundingGoal * 100));
        }
        #endregion
    }
}
