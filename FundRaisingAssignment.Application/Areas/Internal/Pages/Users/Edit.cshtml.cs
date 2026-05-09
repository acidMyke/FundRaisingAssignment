using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

// ─────────────────────────────────────────────────────────────────────────────
// User Story:   UA01 – User Admin – Add and Manage Users    Owner: Khoo Shi Hao Nicholas
// BCE Role:     Boundary
// Description:  Admin form for editing an existing user — username, email,
//               role assignment, and lockout state.
// ─────────────────────────────────────────────────────────────────────────────

namespace FundRaisingAssignment.Application.Areas.Internal.Pages.Users
{
    public class EditModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private static readonly string[] AllRoles =
        {
            ApplicationRole.Names.Admin,
            ApplicationRole.Names.Fundraiser,
            ApplicationRole.Names.PendingFundraiser,
            ApplicationRole.Names.Donor
        };

        public EditModel(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        [BindProperty] public InputModel Input { get; set; } = new();
        public SelectList RoleOptions => new(AllRoles);
        public bool IsLockedOut { get; set; }

        public class InputModel
        {
            public string Id { get; set; } = string.Empty;
            [Required, EmailAddress] public string Email { get; set; } = string.Empty;
            [Required] public string Role { get; set; } = ApplicationRole.Names.Donor;
            public DateTime JoinDate { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound();

            var roles = await _userManager.GetRolesAsync(user);
            Input = new InputModel
            {
                Id = user.Id.ToString(),
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault() ?? ApplicationRole.Names.Donor,
                JoinDate = user.JoinDate
            };
            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByIdAsync(Input.Id);
            if (user == null) return NotFound();

            user.UserName = Input.Email;
            user.Email = Input.Email;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }

            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, Input.Role);

            TempData["SuccessMessage"] = $"User '{user.Email}' updated successfully.";
            return RedirectToPage("Index");
        }

        public async Task<IActionResult> OnPostLockAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            TempData["SuccessMessage"] = $"User '{user.UserName}' has been locked out.";
            return RedirectToPage("Index");
        }

        public async Task<IActionResult> OnPostUnlockAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);
            TempData["SuccessMessage"] = $"User '{user.Email}' has been unlocked.";
            return RedirectToPage("Index");
        }
    }
}