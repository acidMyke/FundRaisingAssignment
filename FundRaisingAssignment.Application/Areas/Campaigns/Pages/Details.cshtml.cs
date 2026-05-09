// Switched off the standalone DonationService onto the canonical
// ICampaignService.DonateAsync; added a switch case for DonationResult.InvalidAmount
// so amount-rule violations surface on the form instead of as a generic error.

using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Campaigns.Pages;

public class DetailsModel(
    ApplicationDbContext context,
    ICampaignService campaignService,
    UserManager<ApplicationUser> userManager) : PageModel
{
    public Campaign Campaign { get; private set; } = default!;

    public bool CanDonate =>
        Campaign.Status == CampaignStatus.Active &&
        (!Campaign.EndDate.HasValue || Campaign.EndDate.Value >= DateTime.UtcNow);

    [BindProperty]
    public DonationInput Input { get; set; } = new();

    public class DonationInput
    {
        [Required]
        [Range(0.01, 1_000_000, ErrorMessage = "Amount must be between 0.01 and 1,000,000.")]
        [Display(Name = "Amount")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        [Display(Name = "Message to the fund raiser (optional)")]
        public string? Message { get; set; }

        [Display(Name = "Donate anonymously")]
        public bool IsAnonymous { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        var campaign = await context.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (campaign is null)
            return NotFound();

        Campaign = campaign;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var campaign = await context.Campaigns
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        if (campaign is null)
            return NotFound();

        Campaign = campaign;

        if (!ModelState.IsValid)
            return Page();

        var user = await userManager.GetUserAsync(User);
        if (user is null)
            return Challenge();

        var result = await campaignService.DonateAsync(
            new MakeDonationInput(
                CampaignId: id,
                Amount: Input.Amount,
                Message: Input.Message,
                IsAnonymous: Input.IsAnonymous,
                UserId: user.Id,
                DonorEmail: user.Email ?? "Unknown"),
            ct);

        switch (result)
        {
            case DonationResult.Success s:
                return RedirectToPage("./DonationConfirmation", new { id = s.Donation.Id });

            case DonationResult.CampaignNotFound:
                return NotFound();

            case DonationResult.CampaignNotActive na:
                ModelState.AddModelError(string.Empty,
                    $"This campaign is currently '{na.CurrentStatus}' and is not accepting donations.");
                return Page();

            case DonationResult.DeadlinePassed:
                ModelState.AddModelError(string.Empty, "The campaign deadline has passed.");
                return Page();

            case DonationResult.InvalidAmount ia:
                ModelState.AddModelError(nameof(Input.Amount), ia.Reason);
                return Page();

            default:
                ModelState.AddModelError(string.Empty, "Unable to process donation.");
                return Page();
        }
    }
}
