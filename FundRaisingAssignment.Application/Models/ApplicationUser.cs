using Microsoft.AspNetCore.Identity;

namespace FundRaisingAssignment.Application.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;

        // User-selectable badges (store BadgeType as int)
        public int? SelectedBadge1Type { get; set; }
        public int? SelectedBadge2Type { get; set; }
    }
}
