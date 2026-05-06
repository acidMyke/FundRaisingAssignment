using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Services
{
    public class CampaignService
    {
        private readonly ApplicationDbContext _context;

        public CampaignService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<Campaign>> SearchCampaigns(string? keyword, string? category, string? location)
        {
            var query = _context.Campaigns.AsQueryable();

            // ✅ CLEAN INPUTS
            keyword = keyword?.Trim();
            category = category?.Trim();
            location = location?.Trim();

            // 🔍 KEYWORD FILTER (STRICT)
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(c =>
                    c.Title.Contains(keyword) ||
                    c.Description.Contains(keyword)
                );
            }

            // 🔍 CATEGORY FILTER
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(c =>
                    (c.Category ?? "").Contains(category)
                );
            }

            // 🔍 LOCATION FILTER
            if (!string.IsNullOrEmpty(location))
            {
                query = query.Where(c =>
                    (c.Location ?? "").Contains(location)
                );
            }

            return await query.ToListAsync();
        }
    }
}