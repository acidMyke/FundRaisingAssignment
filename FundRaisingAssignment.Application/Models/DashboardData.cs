namespace FundRaisingAssignment.Application.Models
{
    public class DashboardData
    {
        public List<KPI> KPIs { get; set; } = new();
        public List<Trend> Trends { get; set; } = new();
    }
}
