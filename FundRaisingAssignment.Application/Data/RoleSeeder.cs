using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;

namespace FundRaiser.Data
{
    public static class RoleSeeder
    {
        public static readonly string[] Roles = { "Admin", "CampaignManager", "Donor" };

        public static async Task SeedRolesAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            foreach (var role in Roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Seed a default Admin account if none exists
            const string adminEmail = "admin@fundraiser.com";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser
                {
                    UserName = "admin",
                    Email = adminEmail,
                    EmailConfirmed = true,
                    JoinDate = DateTime.UtcNow
                };
                var result = await userManager.CreateAsync(admin, "Admin@123!");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }
        }
    }
}
