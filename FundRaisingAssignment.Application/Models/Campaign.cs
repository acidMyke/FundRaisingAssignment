using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FundRaisingAssignment.Application.Models
{
    [Table("Campaigns")]
    public class Campaign
    {
        public Guid Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        // ✅ NEW (for category search)
        [Required]
        [StringLength(50)]
        public string Category { get; set; } = string.Empty;

        // ✅ NEW (for location search)
        [Required]
        [StringLength(100)]
        public string Location { get; set; } = string.Empty;

        [Required]
        [Range(1, double.MaxValue)]
        public decimal TargetAmount { get; set; }

        public decimal CurrentAmount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(200)]
        public string? ShortDescription { get; set; }

        public DateTime StartDate { get; set; } = DateTime.UtcNow;

        public DateTime? EndDate { get; set; }

        public CampaignStatus Status { get; set; } = CampaignStatus.Draft;

        [Required]
        public Guid OwnerId { get; set; }

        [ForeignKey("OwnerId")]
        public ApplicationUser? Owner { get; set; }
    }
}