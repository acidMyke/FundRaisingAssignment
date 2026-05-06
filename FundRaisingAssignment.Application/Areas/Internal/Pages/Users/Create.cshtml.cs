using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace FundRaisingAssignment.Application.Areas.Internal.Pages.Users
{
    public class CreateModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private static readonly string[] AllRoles = { "Admin", "CampaignManager", "Donor" };

        public CreateModel(UserManager<ApplicationUser> userManager) => _userManager = userManager;

        [BindProperty] public InputModel Input { get; set; } = new();
        public SelectList RoleOptions => new(AllRoles);

        public class InputModel
        {
            [Required, EmailAddress] public string Email { get; set; } = string.Empty;
            [Required] public string Role { get; set; } = "Donor";
            [Required, DataType(DataType.Password), StringLength(100, MinimumLength = 8)]
            public string Password { get; set; } = string.Empty;
            [Required, DataType(DataType.Password), Compare("Password", ErrorMessage = "Passwords do not match.")]
            [Display(Name = "Confirm Password")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public void OnGet() { }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                EmailConfirmed = true,
                JoinDate = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, Input.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
                return Page();
            }

            await _userManager.AddToRoleAsync(user, Input.Role);
            TempData["SuccessMessage"] = $"User '{user.Email}' created successfully.";
            return RedirectToPage("Index");
        }
    }
}