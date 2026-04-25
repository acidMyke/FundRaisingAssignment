using Microsoft.AspNetCore.Identity;

namespace FundRaisingAssignment.Application.Models
{
    public class ApplicationRole(string roleName) : IdentityRole<Guid>(roleName)
    {
        public class Names
        {
            public const string Admin = "Admin";
        }
        public static readonly ApplicationRole Admin = new(Names.Admin);
        public static IEnumerable<ApplicationRole> All =>
        [
            Admin
        ];
    }
}
