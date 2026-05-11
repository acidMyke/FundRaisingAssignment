using FundRaisingAssignment.Application.Models;

namespace FundRaisingAssignment.Application.Services
{
    public class AnalyticsService
    {
        public List<Issue> DetectIssues(DashboardFilter filters)
        {
            return new List<Issue>
            {
                new Issue("Low Engagement", "Environment campaigns below target")
            };
        }
    }
}
