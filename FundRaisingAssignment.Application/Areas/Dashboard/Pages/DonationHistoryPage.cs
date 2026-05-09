using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages
{
    [Authorize]
    public class DonationHistoryPageModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public DonationHistoryPageModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public List<Donation> DonationRecords { get; set; } = new();

        [BindProperty]
        public Guid? SelectedDonationId { get; set; }

        public Donation? SelectedDonation { get; set; }
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LoadDonationRecordsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LoadDonationRecordsAsync();
            if (SelectedDonationId.HasValue)
            {
                SelectedDonation = await _context.Donations
                    .Include(r => r.Campaign)
                    .FirstOrDefaultAsync(r => r.Id == SelectedDonationId.Value);

                if (SelectedDonation == null)
                    ErrorMessage = "Failed to retrieve donation details. Please try again.";
            }
            return Page();
        }

        public async Task<IActionResult> OnPostDownloadAsync(Guid PrintOrDownloadId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var donation = await _context.Donations
                .Include(d => d.Campaign)
                .FirstOrDefaultAsync(d => d.Id == PrintOrDownloadId && d.UserId == user.Id);

            if (donation is null) return NotFound();

            var pdfBytes = DonationReceiptPdf.Generate(donation);
            var fileName = $"receipt-{(donation.ReceiptNumber ?? donation.Id.ToString())}.pdf";
            return File(pdfBytes, "application/pdf", fileName);
        }

        private async Task LoadDonationRecordsAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    ErrorMessage = "User not found.";
                    DonationRecords = new List<Donation>();
                    return;
                }

                var records = await _context.Donations
                    .Where(r => r.UserId == user.Id)
                    .Include(r => r.Campaign)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                if (records.Any())
                    DonationRecords = records;
                else
                    ErrorMessage = "No donation records available.";
            }
            catch
            {
                ErrorMessage = "Failed to retrieve donation records. Please try again.";
            }
        }
    }
}
