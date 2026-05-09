using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages
{
    /// <summary>
    /// Full campaign editor – Admin only.
    /// Fundraisers can only edit Goal and Deadline via the Index (Set Funding Goal) page.
    /// </summary>
    [Authorize(Roles = "Admin")]
    public class UpdateCampaignModel(ApplicationDbContext ctx, UserManager<ApplicationUser> um) : PageModel
    {
        private readonly ApplicationDbContext _ctx = ctx;
        private readonly UserManager<ApplicationUser> _um = um;

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string CampaignTitle { get; private set; } = string.Empty;

        public class InputModel
        {
            [Required]
            [StringLength(100)]
            [Display(Name = "Campaign Title")]
            public string Title { get; set; } = string.Empty;

            [Required]
            [StringLength(200)]
            [Display(Name = "Short Description")]
            public string ShortDescription { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Full Description / Purpose")]
            public string Description { get; set; } = string.Empty;

            [Required]
            [Display(Name = "Category")]
            public CampaignCategory Category { get; set; } = CampaignCategory.Other;

            [StringLength(100)]
            [Display(Name = "Location")]
            public string? Location { get; set; }

            [StringLength(500)]
            [Display(Name = "Cover Image URL")]
            public string? CoverImageUrl { get; set; }

            [Required]
            [Range(1, double.MaxValue)]
            [Display(Name = "Funding Goal (USD)")]
            public decimal FundingGoal { get; set; }

            [Required]
            [Display(Name = "Funding Deadline")]
            public DateTime EndDate { get; set; }

            [Required]
            [Display(Name = "Status")]
            public CampaignStatus Status { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var c = await _ctx.Campaigns.FindAsync(Id);
            if (c is null) return NotFound();

            CampaignTitle = c.Title;
            Input = new InputModel
            {
                Title = c.Title,
                ShortDescription = c.ShortDescription,
                Description = c.Description,
                Category = c.Category,
                Location = c.Location,
                CoverImageUrl = c.CoverImageUrl,
                FundingGoal = c.FundingGoal,
                EndDate = c.EndDate ?? DateTime.Today,
                Status = c.Status
            };
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var c = await _ctx.Campaigns.FindAsync(Id);
            if (c is null) return NotFound();

            c.Title = Input.Title;
            c.ShortDescription = Input.ShortDescription;
            c.Description = Input.Description;
            c.Category = Input.Category;
            c.Location = string.IsNullOrWhiteSpace(Input.Location) ? null : Input.Location.Trim();
            c.CoverImageUrl = Input.CoverImageUrl;
            c.FundingGoal = Input.FundingGoal;
            c.TargetAmount = Input.FundingGoal;
            c.EndDate = Input.EndDate;
            c.Status = Input.Status;

            await _ctx.SaveChangesAsync();
            TempData["Success"] = "Campaign updated.";
            return RedirectToPage("./ManageCampaigns");
        }
    }
}
