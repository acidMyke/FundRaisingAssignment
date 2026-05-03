using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FundRaisingAssignment.Application.Models
{
    [Table("Donations")]
    public class Donation
    {
        public Guid Id { get; set; }

        [Required]
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        [Required]
        public Guid UserId { get; set; }
        public ApplicationUser? User { get; set; }

        [Required]
        [Column(TypeName = "numeric(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        public string? Message { get; set; }

        public bool IsAnonymous { get; set; } = false;

        [Required]
        public DonationStatus Status { get; set; } = DonationStatus.Pending;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}