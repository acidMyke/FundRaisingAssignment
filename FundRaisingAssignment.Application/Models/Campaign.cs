using System.ComponentModel.DataAnnotations;

namespace FundRaisingAssignment.Application.Models
{
    public class Campaign
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(1, double.MaxValue)]
        public decimal TargetAmount { get; set; }

        public decimal CurrentAmount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [Required]
        public Guid OwnerId { get; set; }
        public ApplicationUser? Owner { get; set; }
    }
}
