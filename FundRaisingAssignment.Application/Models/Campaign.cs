using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FundRaisingAssignment.Application.Models
{
    [Table("Campaigns")]
    public class Campaign
    {
        public Guid Id { get; set; }

        [Required][StringLength(100)][Display(Name = "Campaign Title")]
        public string Title { get; set; } = string.Empty;

        [Required][StringLength(200)][Display(Name = "Short Description")]
        public string ShortDescription { get; set; } = string.Empty;

        [Required][Display(Name = "Full Description / Purpose")]
        public string Description { get; set; } = string.Empty;

        [Required][Display(Name = "Category")]
        public CampaignCategory Category { get; set; } = CampaignCategory.Other;

        [StringLength(500)][Display(Name = "Cover Image URL")]
        public string? CoverImageUrl { get; set; }

        // ── Financial ─────────────────────────────────────────────────────────
        [Required][Range(1, double.MaxValue)][Display(Name = "Funding Goal")]
        public decimal FundingGoal { get; set; }

        public decimal CurrentAmount { get; set; } = 0;
        public decimal TargetAmount  { get; set; }   // kept for migration compat

        // ── Dates ─────────────────────────────────────────────────────────────
        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
        public DateTime StartDate  { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }

        [Required][Display(Name = "Funding Deadline")]
        public DateTime EndDate { get; set; }

        // ── Status ────────────────────────────────────────────────────────────
        public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

        [StringLength(500)]
        public string? FlagReason { get; set; }

        // ── Rating aggregates ─────────────────────────────────────────────────
        public double AverageRating { get; set; } = 0;
        public int    ReviewCount   { get; set; } = 0;

        // ── Ownership ─────────────────────────────────────────────────────────
        [Required]
        public Guid OwnerId { get; set; }
        public ApplicationUser? Owner { get; set; }

        // ── Derived helpers ───────────────────────────────────────────────────
        public bool IsPubliclyVisible => Status == CampaignStatus.Active;
        public bool AcceptsDonations  => Status == CampaignStatus.Active;

        // ══════════════════════════════════════════════════════════════════════
        // Domain methods – match BCE diagrams exactly
        // ══════════════════════════════════════════════════════════════════════

        /// <summary>Fundraiser submits a Draft campaign for admin review.</summary>
        public void SubmitForReview()
        {
            if (Status != CampaignStatus.Draft)
                throw new InvalidOperationException("Only Draft campaigns can be submitted for review.");
            Status = CampaignStatus.PendingReview;
        }

        /// <summary>
        /// Admin publishes a PendingReview campaign → Active.
        /// BCE Diagram 1 – saveGoalAndDeadline result becomes publicly visible.
        /// </summary>
        public void PublishCampaign()
        {
            if (Status != CampaignStatus.PendingReview)
                throw new InvalidOperationException("Only campaigns pending review can be published.");
            Status      = CampaignStatus.Active;
            PublishedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Admin flags an Active campaign for later review.
        /// Campaign is removed from public listing; fundraiser must wait.
        /// </summary>
        public void FlagCampaignByAdmin(string reason)
        {
            Status     = CampaignStatus.Flagged;
            FlagReason = reason;
        }

        /// <summary>
        /// Admin pauses a campaign temporarily.
        /// Removed from public listing; no donations accepted.
        /// </summary>
        public void PauseCampaign(string reason)
        {
            Status     = CampaignStatus.Paused;
            FlagReason = reason;
        }

        /// <summary>
        /// Admin terminates a campaign permanently.
        /// Removed from all public views; fundraiser notified.
        /// </summary>
        public void TerminateCampaign(string reason)
        {
            Status     = CampaignStatus.Cancelled;
            FlagReason = reason;
        }

        /// <summary>
        /// Admin releases a Flagged or Paused campaign back to Active.
        /// Campaign returns to public listing and accepts donations again.
        /// </summary>
        public void ReleaseCampaign()
        {
            if (Status != CampaignStatus.Flagged && Status != CampaignStatus.Paused)
                throw new InvalidOperationException("Only Flagged or Paused campaigns can be released.");
            Status     = CampaignStatus.Active;
            FlagReason = null;
        }

        /// <summary>BCE Diagram 1 – updates goal + deadline (fundraiser always allowed).</summary>
        public void UpdateGoalAndDeadline(decimal goalAmount, DateTime deadlineDate)
        {
            FundingGoal  = goalAmount;
            TargetAmount = goalAmount;
            EndDate      = deadlineDate;
        }

        /// <summary>BCE Diagram 2 – approve after review → Active.</summary>
        public void ApproveCampaignStatus()
        {
            Status     = CampaignStatus.Active;
            FlagReason = null;
        }

        /// <summary>BCE Diagram 2 – remove after review → Cancelled.</summary>
        public void RemoveCampaignStatus(string removalReason)
        {
            Status     = CampaignStatus.Cancelled;
            FlagReason = removalReason;
        }

        /// <summary>
        /// Recalculates rating aggregates and auto-flags if avg ≤ 2.0 with ≥ 3 reviews.
        /// </summary>
        public void RecalculateRating(double newAverage, int newCount)
        {
            AverageRating = Math.Round(newAverage, 2);
            ReviewCount   = newCount;

            if (Status == CampaignStatus.Active && ReviewCount >= 3 && AverageRating <= 2.0)
            {
                Status     = CampaignStatus.Flagged;
                FlagReason = $"Auto-flagged: average rating {AverageRating:F1} stars ({ReviewCount} reviews).";
            }
        }
    }
}
