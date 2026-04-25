using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Security
{
    public class MinimumJoinTimeRequirement(int minimumDays) : IAuthorizationRequirement
    {
        public int MinimumDays { get; } = minimumDays;
    }

    public class MinimumJoinTimeHandler(UserManager<ApplicationUser> userManager) : AuthorizationHandler<MinimumJoinTimeRequirement>
    {
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, MinimumJoinTimeRequirement requirement)
        {
            var user = await _userManager.GetUserAsync(context.User);
            if (user == null)
            {
                return;
            }

            var timeSinceJoin = DateTime.UtcNow - user.JoinDate;
            if (timeSinceJoin.TotalDays >= requirement.MinimumDays)
            {
                context.Succeed(requirement);
            }
        }
    }
}
