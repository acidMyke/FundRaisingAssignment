using Microsoft.AspNetCore.Identity;

namespace FundRaisingAssignment.Application.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;
        public bool ReceiveCampaignDigest { get; set; } = true;
        public DateTime? UnsubscribeCooldownUntil { get; set; }
        public DateTime? LastCampaignUpdateSent { get; set; }
        public bool IsEmailBounced { get; set; }
    }
}
