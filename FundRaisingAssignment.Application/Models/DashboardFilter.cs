namespace FundRaisingAssignment.Application.Models
{
    public class DashboardFilter
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Region { get; set; } = string.Empty;

        public bool IsValid() => StartDate <= EndDate;
    }
}
