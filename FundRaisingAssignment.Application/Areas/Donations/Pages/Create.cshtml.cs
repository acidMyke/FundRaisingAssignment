// Security-critical rewrite: the previous version model-bound the entire Donation
// entity from form input (over-posting risk: a client could supply Status, UserId,
// ReceiptNumber, etc. via hidden fields). Now binds a tiny Input DTO instead and
// funnels through the canonical ICampaignService.DonateAsync, which sets
// server-controlled fields (UserId, Status, ReceiptNumber, CreatedAt) itself.

using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Donations.Pages
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ICampaignService _campaignService;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateModel(
            ApplicationDbContext context,
            ICampaignService campaignService,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _campaignService = campaignService;
            _userManager = userManager;
        }

        [BindProperty]
        public CreateInput Input { get; set; } = new();

        public Campaign? Campaign { get; set; }

        public class CreateInput
        {
            [Required]
            [Range(0.01, 1_000_000, ErrorMessage = "Amount must be between 0.01 and 1,000,000.")]
            [Display(Name = "Amount")]
            public decimal Amount { get; set; }

            [StringLength(500)]
            [Display(Name = "Message (optional)")]
            public string? Message { get; set; }

            [Display(Name = "Donate anonymously")]
            public bool IsAnonymous { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(Guid campaignId)
        {
            Campaign = await _context.Campaigns.FindAsync(campaignId);
            if (Campaign == null)
                return NotFound();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid campaignId, CancellationToken ct)
        {
            Campaign = await _context.Campaigns.FindAsync(campaignId);
            if (Campaign == null)
                return NotFound();

            if (!ModelState.IsValid)
                return Page();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var result = await _campaignService.DonateAsync(
                new MakeDonationInput(
                    CampaignId: campaignId,
                    Amount: Input.Amount,
                    Message: Input.Message,
                    IsAnonymous: Input.IsAnonymous,
                    UserId: user.Id,
                    DonorEmail: user.Email ?? "Unknown"),
                ct);

            switch (result)
            {
                case DonationResult.Success:
                    return RedirectToPage("/Details", new { area = "Campaigns", id = campaignId });

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
}
