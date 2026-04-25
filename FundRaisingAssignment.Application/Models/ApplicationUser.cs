using Microsoft.AspNetCore.Identity;

namespace FundRaisingAssignment.Application.Models
{
    public class ApplicationUser : IdentityUser<Guid>
    {
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;
    }
}
