using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FundRaisingAssignment.Application.Models
{
    /// <summary>
    /// A monetary donation made by a donor to an Active campaign.
    /// Amount is always positive – enforced at DB level (CHECK constraint via migration)
    /// and at application level via [Range] + service validation.
    /// </summary>
    [Table("Donations")]
    public class Donation
    {
        public Guid DonationId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        /// <summary>Null when donated anonymously or by a non-registered guest.</summary>
        public Guid? DonorId { get; set; }
        public ApplicationUser? Donor { get; set; }

        /// <summary>Cached so it displays even after account deletion.</summary>
        [StringLength(256)]
        public string DonorEmail { get; set; } = "Anonymous";

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Donation amount must be greater than $0.")]
        [Display(Name = "Donation Amount (USD)")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        [Display(Name = "Message (optional)")]
        public string? Message { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsAnonymous { get; set; } = false;
    }
}
