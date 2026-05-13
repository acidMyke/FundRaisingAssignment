using System.Collections.Generic;
using System.Threading.Tasks;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages
{
    [Authorize]
    public class MyBadgesModel : PageModel
    {
        private readonly BadgeService _badgeService;
        private readonly UserManager<ApplicationUser> _userManager;
        public List<Badge> Badges { get; set; } = new();

        [BindProperty]
        public int? SelectedBadge1Type { get; set; }
        [BindProperty]
        public int? SelectedBadge2Type { get; set; }
        public string? SaveMessage { get; set; }

        public MyBadgesModel(BadgeService badgeService, UserManager<ApplicationUser> userManager)
        {
            _badgeService = badgeService;
            _userManager = userManager;
        }

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var progress = await _badgeService.GetUserBadgesAsync(user.Id);
                Badges = progress.Badges;
                SelectedBadge1Type = user.SelectedBadge1Type;
                SelectedBadge2Type = user.SelectedBadge2Type;
            }
        }

        public async Task OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var progress = await _badgeService.GetUserBadgesAsync(user.Id);
                Badges = progress.Badges;

                // Only allow selection of badges the user has
                var unlockedTypes = Badges.Select(b => (int)b.Type).ToHashSet();
                if ((SelectedBadge1Type.HasValue && !unlockedTypes.Contains(SelectedBadge1Type.Value)) ||
                    (SelectedBadge2Type.HasValue && !unlockedTypes.Contains(SelectedBadge2Type.Value)))
                {
                    SaveMessage = "Invalid badge selection.";
                    SelectedBadge1Type = user.SelectedBadge1Type;
                    SelectedBadge2Type = user.SelectedBadge2Type;
                    return;
                }
                // Prevent selecting the same badge twice
                if (SelectedBadge1Type.HasValue && SelectedBadge2Type.HasValue && SelectedBadge1Type == SelectedBadge2Type)
                {
                    SaveMessage = "Please select two different badges.";
                    return;
                }
                user.SelectedBadge1Type = SelectedBadge1Type;
                user.SelectedBadge2Type = SelectedBadge2Type;
                await _userManager.UpdateAsync(user);
                SaveMessage = "Badge display selection saved!";
            }
        }
    }
}
