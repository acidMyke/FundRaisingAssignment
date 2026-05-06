using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages
{
    [Authorize]
    public class CreateCampaignModel(ICampaignService svc, UserManager<ApplicationUser> um) : PageModel
    {
        private readonly ICampaignService _svc = svc;
        private readonly UserManager<ApplicationUser> _um = um;

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required][StringLength(100)][Display(Name = "Campaign Title")]
            public string Title { get; set; } = string.Empty;

            [Required][StringLength(200)][Display(Name = "Short Description")]
            public string ShortDescription { get; set; } = string.Empty;

            [Required][Display(Name = "Full Description / Purpose")]
            public string Description { get; set; } = string.Empty;

            [Required][Display(Name = "Category")]
            public CampaignCategory Category { get; set; } = CampaignCategory.Other;

            [StringLength(500)][Display(Name = "Cover Image URL (optional)")]
            public string? CoverImageUrl { get; set; }

            [Required][Range(1, double.MaxValue, ErrorMessage = "Funding goal must be greater than $0.")]
            [Display(Name = "Funding Goal (USD)")]
            public decimal FundingGoal { get; set; }

            [Required][Display(Name = "Funding Deadline")][DataType(DataType.Date)]
            public DateTime EndDate { get; set; } = DateTime.Today.AddDays(30);
        }

        public IActionResult OnGet()
        {
            Input = new InputModel { EndDate = DateTime.Today.AddDays(30) };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (Input.EndDate.Date <= DateTime.Today)
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.EndDate)}",
                    "Funding deadline must be a future date.");

            if (!ModelState.IsValid) return Page();

            var user = await _um.GetUserAsync(User);
            if (user is null) return Challenge();

            // Status is always set to Draft by the service – fundraiser submits for review separately
            var campaign = new Campaign
            {
                Title            = Input.Title,
                ShortDescription = Input.ShortDescription,
                Description      = Input.Description,
                Category         = Input.Category,
                CoverImageUrl    = Input.CoverImageUrl,
                FundingGoal      = Input.FundingGoal,
                StartDate        = DateTime.Now,
                EndDate          = Input.EndDate,
                OwnerId          = user.Id
            };

            var saved = await _svc.CreateCampaignAsync(campaign);
            TempData["Success"] = $"Campaign \"{saved.Title}\" created as a draft. Submit it for review when you're ready.";
            return RedirectToPage("/Campaigns", new { area = "Dashboard" });
        }
    }
}
