using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Internal.Pages.Users
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public IndexModel(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public List<UserListItem> Users { get; set; } = new();

        [BindProperty(SupportsGet = true)] public string? SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)] public string? RoleFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }

        [TempData] public string? SuccessMessage { get; set; }
        [TempData] public string? ErrorMessage { get; set; }

        public record UserListItem(Guid Id, string UserName, string Email,
            string Role, bool IsLockedOut, DateTime JoinDate);

        public async Task OnGetAsync()
        {
            var allUsers = await _userManager.Users
                .OrderByDescending(u => u.JoinDate)
                .ToListAsync();

            var list = new List<UserListItem>();
            foreach (var u in allUsers)
            {
                var roles = await _userManager.GetRolesAsync(u);
                var lockedOut = u.LockoutEnd.HasValue && u.LockoutEnd > DateTimeOffset.UtcNow;
                list.Add(new UserListItem(u.Id, u.UserName ?? "", u.Email ?? "",
                    roles.FirstOrDefault() ?? "—", lockedOut, u.JoinDate));
            }

            if (!string.IsNullOrWhiteSpace(SearchTerm))
                list = list.Where(u => u.Email.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(RoleFilter))
                list = list.Where(u => u.Role == RoleFilter).ToList();
            if (StatusFilter == "locked")
                list = list.Where(u => u.IsLockedOut).ToList();
            else if (StatusFilter == "active")
                list = list.Where(u => !u.IsLockedOut).ToList();

            Users = list;
        }
    }
}