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

        [Required][Display(Name = "Description")]
        public string Description { get; set; } = string.Empty;

        [StringLength(200)][Display(Name = "Short Description")]
        public string? ShortDescription { get; set; }

        // ── Category: enum (Josh) ──────────────────────────────────────────────
        [Required][Display(Name = "Category")]
        public CampaignCategory Category { get; set; } = CampaignCategory.Other;

        // ── Location: string (Karthik – used in search) ───────────────────────
        [StringLength(100)][Display(Name = "Location")]
        public string? Location { get; set; }

        [StringLength(500)][Display(Name = "Cover Image URL")]
        public string? CoverImageUrl { get; set; }

        // ── Financial ─────────────────────────────────────────────────────────
        [Required][Range(1, double.MaxValue)][Display(Name = "Funding Goal")]
        public decimal FundingGoal { get; set; }

        [Required][Range(1, double.MaxValue)][Display(Name = "Target Amount")]
        public decimal TargetAmount { get; set; }   // kept for Karthik compat

        public decimal CurrentAmount { get; set; } = 0;

        // ── Dates ─────────────────────────────────────────────────────────────
        public DateTime CreatedAt  { get; set; } = DateTime.UtcNow;
        public DateTime StartDate  { get; set; } = DateTime.UtcNow;
        public DateTime? PublishedAt { get; set; }
        public DateTime? EndDate   { get; set; }   // nullable for Karthik DB compat

        // ── Status ────────────────────────────────────────────────────────────
        public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

        [StringLength(500)]
        public string? FlagReason { get; set; }

        // ── Rating aggregates (Josh) ──────────────────────────────────────────
        public double AverageRating { get; set; } = 0;
        public int    ReviewCount   { get; set; } = 0;

        // ── Ownership ─────────────────────────────────────────────────────────
        [Required]
        public Guid OwnerId { get; set; }
        public ApplicationUser? Owner { get; set; }

        // ── Derived helpers ───────────────────────────────────────────────────
        public bool IsPubliclyVisible => Status == CampaignStatus.Active;
        public bool AcceptsDonations  => Status == CampaignStatus.Active;

        // ── Domain methods ────────────────────────────────────────────────────

        public void SubmitForReview()
        {
            if (Status != CampaignStatus.Draft)
                throw new InvalidOperationException("Only Draft campaigns can be submitted for review.");
            Status = CampaignStatus.PendingReview;
        }

        public void PublishCampaign()
        {
            if (Status != CampaignStatus.PendingReview)
                throw new InvalidOperationException("Only campaigns pending review can be published.");
            Status      = CampaignStatus.Active;
            PublishedAt = DateTime.UtcNow;
        }

        public void FlagCampaignByAdmin(string reason)
        {
            Status     = CampaignStatus.Flagged;
            FlagReason = reason;
        }

        public void PauseCampaign(string reason)
        {
            Status     = CampaignStatus.Paused;
            FlagReason = reason;
        }

        public void TerminateCampaign(string reason)
        {
            Status     = CampaignStatus.Cancelled;
            FlagReason = reason;
        }

        public void ReleaseCampaign()
        {
            if (Status != CampaignStatus.Flagged && Status != CampaignStatus.Paused)
                throw new InvalidOperationException("Only Flagged or Paused campaigns can be released.");
            Status     = CampaignStatus.Active;
            FlagReason = null;
        }

        /// <summary>BCE Diagram 1 – updates goal + deadline.</summary>
        public void UpdateGoalAndDeadline(decimal goalAmount, DateTime deadlineDate)
        {
            FundingGoal  = goalAmount;
            TargetAmount = goalAmount;
            EndDate      = deadlineDate;
        }

        public void ApproveCampaignStatus()
        {
            Status     = CampaignStatus.Active;
            FlagReason = null;
        }

        public void RemoveCampaignStatus(string removalReason)
        {
            Status     = CampaignStatus.Cancelled;
            FlagReason = removalReason;
        }

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
