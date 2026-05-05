using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Campaigns.Pages;

public class IndexModel(ApplicationDbContext context) : PageModel
{
    public IList<Campaign> Campaigns { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        Campaigns = await context.Campaigns
            .Where(c => c.Status == CampaignStatus.Active &&
                        (c.EndDate == null || c.EndDate >= now))
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
    }
}
