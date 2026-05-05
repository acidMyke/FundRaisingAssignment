using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Campaigns.Pages
{
    public class DetailsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DetailsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public Campaign Campaign { get; set; }

        public List<LeaderboardEntry> TopDonorsByAmount { get; set; } = new();
        public List<LeaderboardEntry> TopDonorsByFrequency { get; set; } = new();

        public class LeaderboardEntry
        {
            public Guid DoneeId { get; set; }
            public string? UserName { get; set; }
            public decimal Amount { get; set; }
            public int Count { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            Campaign = await _context.Campaigns.Include(c => c.Owner).FirstOrDefaultAsync(c => c.Id == id);
            if (Campaign == null)
            {
                return NotFound();
            }

            // Top 10 by highest single donation
            TopDonorsByAmount = await _context.DonationRecords
                .Where(d => d.CampaignId == id)
                .OrderByDescending(d => d.Amount)
                .Take(10)
                .Select(d => new LeaderboardEntry
                {
                    DoneeId = d.DoneeId,
                    UserName = _context.Users.Where(u => u.Id == d.DoneeId).Select(u => u.UserName).FirstOrDefault(),
                    Amount = d.Amount,
                    Count = 1
                })
                .ToListAsync();

            // Top 10 by donation frequency
            TopDonorsByFrequency = await _context.DonationRecords
                .Where(d => d.CampaignId == id)
                .GroupBy(d => d.DoneeId)
                .Select(g => new LeaderboardEntry
                {
                    DoneeId = g.Key,
                    UserName = _context.Users.Where(u => u.Id == g.Key).Select(u => u.UserName).FirstOrDefault(),
                    Amount = g.Sum(x => x.Amount),
                    Count = g.Count()
                })
                .OrderByDescending(e => e.Count)
                .ThenByDescending(e => e.Amount)
                .Take(10)
                .ToListAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(Guid id)
        {
            // Redirect to donation page or handle donation logic here
            return RedirectToPage("/Create", new { area = "Donation", campaignId = id });
        }
    }
}
