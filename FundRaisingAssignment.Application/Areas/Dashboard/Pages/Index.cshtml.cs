using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages
{
    [Authorize(Roles = ApplicationRole.Names.Fundraiser)]
    public class IndexModel(ApplicationDbContext context, UserManager<ApplicationUser> userManager) : PageModel
    {
        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<ApplicationUser> _userManager = userManager;

        public IList<Campaign> Campaigns { get; set; } = default!;

        public async Task OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                Campaigns = await _context.Campaigns
                    .Where(c => c.OwnerId == user.Id)
                    .OrderByDescending(c => c.CreatedAt)
                    .ToListAsync();
            }
            else
            {
                Campaigns = [];
            }
        }
    }
}
