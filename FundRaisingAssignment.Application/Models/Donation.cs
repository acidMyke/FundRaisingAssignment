using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FundRaisingAssignment.Application.Models
{
    [Table("Donations")]
    public class Donation
    {
        // ── Primary key (Karthik uses "Id", Josh uses "DonationId" – unified as "Id") ─
        public Guid Id { get; set; }

        [Required]
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        // ── Donor reference (nullable to support guest donations from Josh flow) ──
        public Guid? UserId { get; set; }       // Karthik: DonationHistoryPage filter
        public ApplicationUser? User { get; set; }

        // ── Josh fields ────────────────────────────────────────────────────────
        [StringLength(256)]
        public string DonorEmail { get; set; } = "Anonymous";

        public bool IsAnonymous { get; set; } = false;

        // ── Both ──────────────────────────────────────────────────────────────
        [Required]
        [Column(TypeName = "numeric(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than $0.")]
        [Display(Name = "Donation Amount (USD)")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        [Display(Name = "Message (optional)")]
        public string? Message { get; set; }

        // ── Karthik extra fields ───────────────────────────────────────────────
        public DonationStatus Status { get; set; } = DonationStatus.Completed;

        [StringLength(50)]
        public string? ReceiptNumber { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "Other";

        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
