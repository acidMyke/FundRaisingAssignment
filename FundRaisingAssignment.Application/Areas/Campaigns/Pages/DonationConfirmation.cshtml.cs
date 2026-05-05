using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Campaigns.Pages;

[Authorize]
public class DonationConfirmationModel(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public Donation Donation { get; private set; } = default!;
    public Campaign Campaign { get; private set; } = default!;
    public bool GoalReached => Campaign.Status == CampaignStatus.Completed;

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var donation = await context.Donations
            .AsNoTracking()
            .Include(d => d.Campaign)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (donation is null || donation.Campaign is null)
            return NotFound();

        var user = await userManager.GetUserAsync(User);
        if (user is null || donation.UserId != user.Id)
            return Forbid();

        Donation = donation;
        Campaign = donation.Campaign;
        return Page();
    }
}
