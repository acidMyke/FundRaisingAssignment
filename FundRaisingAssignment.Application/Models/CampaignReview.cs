using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FundRaisingAssignment.Application.Models
{
    /// <summary>
    /// Donor review for a campaign: star rating + optional comment.
    /// Any review of ≤ 2 stars on an Active campaign auto-flags it for admin review.
    /// </summary>
    [Table("CampaignReviews")]
    public class CampaignReview
    {
        public Guid ReviewId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        /// <summary>Identity user who left the review.</summary>
        [Required]
        public Guid ReviewerId { get; set; }
        public ApplicationUser? Reviewer { get; set; }

        /// <summary>Cached email so it displays even if the user is deleted.</summary>
        [StringLength(256)]
        public string ReviewerEmail { get; set; } = string.Empty;

        /// <summary>Star rating 1 – 5.</summary>
        [Required]
        [Range(1, 5, ErrorMessage = "Please choose a star rating between 1 and 5.")]
        [Display(Name = "Rating")]
        public int Stars { get; set; }

        [StringLength(1000)]
        [Display(Name = "Comment (optional)")]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
