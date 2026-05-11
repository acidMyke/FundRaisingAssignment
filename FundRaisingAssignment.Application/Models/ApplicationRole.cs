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
            public const string Fundraiser = "Fundraiser";
            public const string PendingFundraiser = "PendingFundraiser";
            public const string CampaignManager = "CampaignManager";
            public const string Donor = "Donor";
            public const string PlatformManager = "PlatformManager";  
            
        }
        public static readonly ApplicationRole Admin = new(Names.Admin);
        public static readonly ApplicationRole Fundraiser = new(Names.Fundraiser);
        public static readonly ApplicationRole PendingFundraiser = new(Names.PendingFundraiser);
        public static readonly ApplicationRole Donor = new(Names.Donor);
        public static readonly ApplicationRole PlatformManager = new(Names.PlatformManager);
        public static IEnumerable<ApplicationRole> All =>
        [
            Admin,
            Fundraiser,
            PendingFundraiser,
            Donor,
            PlatformManager
        ];
    }
}
