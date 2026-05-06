using System.ComponentModel.DataAnnotations;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages
{
    [Authorize]
    public class CreateCampaignModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager) : PageModel
    {
        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

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
            [StringLength(50)]
            public string Category { get; set; } = string.Empty;

            [Required]
            [StringLength(100)]
            public string Location { get; set; } = string.Empty;

        }

        public IActionResult OnGet()
        {
            Input = new InputModel { StartDate = DateTime.Now };
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

            var campaign = new Campaign
            {
                Id = Guid.NewGuid(),
                Title = Input.Title,
                ShortDescription = Input.ShortDescription,
                Description = Input.Description,
                TargetAmount = Input.TargetAmount,
                CurrentAmount = 0,
                StartDate = Input.StartDate,
                EndDate = Input.EndDate,
                Status = CampaignStatus.Draft, // Default status
                CreatedAt = DateTime.UtcNow,
                OwnerId = user.Id,
                Category = Input.Category,
                Location = Input.Location
            };

            _context.Campaigns.Add(campaign);
            await _context.SaveChangesAsync();

            return RedirectToPage("./Index");
        }
    }
}
