using Microsoft.AspNetCore.Identity;

namespace FundRaisingAssignment.Application.Models
{
    public class ApplicationRole : IdentityRole<Guid>
    {
        public ApplicationRole() : base()
        {
        }

        public ApplicationRole(string roleName) : base(roleName)
        {
        }

        public class Names
        {
            public const string Admin = "Admin";
            public const string CampaignManager = "CampaignManager";  // ADD
            public const string Donor = "Donor";                       // ADD
        }
        public static readonly ApplicationRole Admin = new(Names.Admin);
        public static IEnumerable<ApplicationRole> All =>
        [
            Admin
        ];
    }
}
