using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FundRaisingAssignment.Application.Models
{
    [Table("CampaignVisits")]
    public class CampaignVisit
    {
        [Key]
        public Guid Id { get; set; }

        public Guid CampaignId { get; set; }
        
        [ForeignKey("CampaignId")]
        public Campaign? Campaign { get; set; }

        public Guid UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        public DateTime VisitDate { get; set; } = DateTime.UtcNow;
    }
}
