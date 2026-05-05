using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace FundRaisingAssignment.Application.Security
{
    public class MinimumJoinTimeRequirement(TimeSpan minimumJoinTime) : IAuthorizationRequirement
    {
        public TimeSpan MinimumJoinTime { get; } = minimumJoinTime;
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

            if (DateTime.UtcNow - user.JoinDate >= requirement.MinimumJoinTime)
            {
                context.Succeed(requirement);
            }
        }
    }
}
