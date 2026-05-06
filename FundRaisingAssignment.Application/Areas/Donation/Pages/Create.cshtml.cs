using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Donation.Pages
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public CreateModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [BindProperty]
        public DonationRecord Donation { get; set; } = default!;
        public Campaign? Campaign { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid campaignId)
        {
            Campaign = await _context.Campaigns.FindAsync(campaignId);
            if (Campaign == null)
                return NotFound();
            Donation = new DonationRecord
            {
                CampaignId = campaignId,
                ReceiptNumber = Guid.NewGuid().ToString().Substring(0, 8).ToUpper() // Temporary value for model binding
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid campaignId)
        {
            Campaign = await _context.Campaigns.FindAsync(campaignId);
            if (Campaign == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                TempData["Debug"] = "ModelState is invalid: " + string.Join(", ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Debug"] = "User is not authenticated.";
                return Challenge();
            }
            Donation.DoneeId = user.Id;
            Donation.Date = DateTime.UtcNow;
            Donation.CampaignId = campaignId;
            Donation.ReceiptNumber = Guid.NewGuid().ToString().Substring(0, 8).ToUpper();
            _context.DonationRecords.Add(Donation);
            Campaign.CurrentAmount += Donation.Amount;
            await _context.SaveChangesAsync();
            TempData["Debug"] = "Donation created and redirecting.";
            return RedirectToPage("/Details", new { area = "Campaigns", id = campaignId });
        }
    }
}
