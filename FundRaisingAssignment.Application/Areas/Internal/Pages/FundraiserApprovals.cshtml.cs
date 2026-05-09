using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Internal.Pages;

[Authorize(Roles = ApplicationRole.Names.Admin)]
public class FundraiserApprovalsModel(UserManager<ApplicationUser> userManager) : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public IList<ApplicationUser> PendingUsers { get; private set; } = [];

    [TempData]
    public string? StatusMessage { get; set; }

    public async Task OnGetAsync()
    {
        var users = await _userManager.GetUsersInRoleAsync(ApplicationRole.Names.PendingFundraiser);
        PendingUsers = users.OrderBy(u => u.JoinDate).ToList();
    }

    public async Task<IActionResult> OnPostApproveAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            StatusMessage = "User not found.";
            return RedirectToPage();
        }

        await _userManager.RemoveFromRoleAsync(user, ApplicationRole.Names.PendingFundraiser);
        await _userManager.AddToRoleAsync(user, ApplicationRole.Names.Fundraiser);

        StatusMessage = $"Approved {user.Email} as a fundraiser.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(Guid id)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user is null)
        {
            StatusMessage = "User not found.";
            return RedirectToPage();
        }

        await _userManager.RemoveFromRoleAsync(user, ApplicationRole.Names.PendingFundraiser);

        StatusMessage = $"Rejected {user.Email}'s fundraiser application.";
        return RedirectToPage();
    }
}
