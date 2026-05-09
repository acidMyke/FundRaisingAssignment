using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Services;
using FundRaisingAssignment.Application.Models;
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
        private readonly DonationService _donationService;
        private readonly UserManager<ApplicationUser> _userManager;

        public DonationHistoryPageModel(DonationService donationService, UserManager<ApplicationUser> userManager)
        {
            _donationService = donationService;
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
                // Use DonationService to get all donations for the user, then find the selected one
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var records = await _donationService.GetDonationsByUserAsync(user.Id);
                    SelectedDonation = records.FirstOrDefault(r => r.Id == SelectedDonationId.Value);
                    if (SelectedDonation == null)
                        ErrorMessage = "Failed to retrieve donation details. Please try again.";
                }
                else
                {
                    ErrorMessage = "User not found.";
                }
            }
            return Page();
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

                var records = await _donationService.GetDonationsByUserAsync(user.Id);
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
