using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Internal.Pages.Users
{
    public class DeleteModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        public DeleteModel(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        public string UserName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound();
            UserId = id.ToString();
            UserName = user.UserName ?? "";
            Email = user.Email ?? "";
            return Page();
        }

        // Soft: lock out the account
        public async Task<IActionResult> OnPostLockAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            TempData["SuccessMessage"] = $"User '{user.UserName}' has been locked out.";
            return RedirectToPage("Index");
        }

        // Hard: permanently delete
        public async Task<IActionResult> OnPostDeleteAsync(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var name = user.UserName;
            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                TempData["ErrorMessage"] = "Failed to delete user.";
                return RedirectToPage("Delete", new { id });
            }
            TempData["SuccessMessage"] = $"User '{name}' permanently deleted.";
            return RedirectToPage("Index");
        }
    }
}