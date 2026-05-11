namespace FundRaisingAssignment.Application.Models
{
    public class DashboardViewModel
    {
        public DashboardFilter Filters { get; set; } = new DashboardFilter();
        public List<KPI> KPIs { get; set; } = new();
        public List<Trend> Trends { get; set; } = new();
        public List<Issue> Issues { get; set; } = new();
    }
}
