using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages
{
    [Authorize]
    public class UpdateCampaignModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager) : PageModel
    {
        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        [BindProperty(SupportsGet = true)]
        public Guid Id { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(100)]
            public string Title { get; set; } = string.Empty;

            [Required]
            public string Description { get; set; } = string.Empty;

            [StringLength(200)]
            [Display(Name = "Short Description")]
            public string? ShortDescription { get; set; }

            [Required]
            [Range(1, double.MaxValue, ErrorMessage = "Target Amount must be greater than 0.")]
            [Display(Name = "Target Amount")]
            public decimal TargetAmount { get; set; }

            [Required]
            [Display(Name = "Start Date")]
            public DateTime StartDate { get; set; } = DateTime.Now;

            [Display(Name = "End Date")]
            public DateTime? EndDate { get; set; }

            [Required]
            public CampaignStatus Status { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == Id && c.OwnerId == user.Id);

            if (campaign == null)
            {
                return NotFound();
            }

            Input = new InputModel
            {
                Title = campaign.Title,
                ShortDescription = campaign.ShortDescription,
                Description = campaign.Description,
                TargetAmount = campaign.TargetAmount,
                StartDate = campaign.StartDate,
                EndDate = campaign.EndDate,
                Status = campaign.Status
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == Id && c.OwnerId == user.Id);

            if (campaign == null)
            {
                return NotFound();
            }

            campaign.Title = Input.Title;
            campaign.ShortDescription = Input.ShortDescription;
            campaign.Description = Input.Description;
            campaign.TargetAmount = Input.TargetAmount;
            campaign.StartDate = Input.StartDate;
            campaign.EndDate = Input.EndDate;
            campaign.Status = Input.Status;

            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
