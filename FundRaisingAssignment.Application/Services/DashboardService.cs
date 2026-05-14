using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Services
{
    public class DashboardService
    {
        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        public DashboardData GetDashboardData(string type, DashboardFilter filters)
        {
            return new DashboardData
            {
                KPIs = CalculateKpis(filters),
                Trends = DetectTrends(filters)
            };
        }

        public List<KPI> CalculateKpis(DashboardFilter filters)
        {
            var campaigns = _context.Campaigns
                .Where(c => c.CreatedAt >= filters.StartDate && c.CreatedAt <= filters.EndDate);

            // ✅ Category filter: parse before query
            if (!string.IsNullOrEmpty(filters.Category))
            {
                if (Enum.TryParse<CampaignCategory>(filters.Category, true, out var category))
                {
                    campaigns = campaigns.Where(c => c.Category == category);
                }
                else
                {
                    // If parsing fails, exclude everything
                    campaigns = campaigns.Where(c => false);
                }
            }

            // ✅ Region filter: normalize filter string first
            if (!string.IsNullOrEmpty(filters.Region))
            {
                var regionNormalized = filters.Region.ToLower();
                campaigns = campaigns.Where(c =>
                    c.Location != null && c.Location.ToLower() == regionNormalized);
            }

            var totalRaised = campaigns.Sum(c => c.CurrentAmount);
            var campaignCount = campaigns.Count();
            var averageRaised = campaignCount > 0 ? totalRaised / campaignCount : 0;

            return new List<KPI>
            {
                new KPI("Total Raised", totalRaised),
                new KPI("Campaign Count", campaignCount),
                new KPI("Average Raised per Campaign", averageRaised)
            };
        }

        public List<Trend> DetectTrends(DashboardFilter filters)
        {
            var donations = _context.Donations
                .Where(d => d.CreatedAt >= filters.StartDate && d.CreatedAt <= filters.EndDate);

            var monthlyTotals = donations
                .GroupBy(d => new { d.CreatedAt.Year, d.CreatedAt.Month })
                .Select(g => new
                {
                    Month = new DateTime(g.Key.Year, g.Key.Month, 1),
                    Total = g.Sum(d => d.Amount)
                })
                .OrderBy(x => x.Month) // ✅ ensures chronological order
                .ToList();

            return monthlyTotals.Select(m => new Trend(
                m.Month.ToString("MMM yyyy"),
                $"Total donations in {m.Month:MMM yyyy}",
                m.Total
            )).ToList();
        }
    }
}
