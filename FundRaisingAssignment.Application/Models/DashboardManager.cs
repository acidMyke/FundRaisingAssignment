namespace FundRaisingAssignment.Application.Models
{
    public class DashboardManager
    {
        public int ManagerID { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = "PlatformManager";
        public bool Login { get; set; }

        public bool HasPermission() => Role == "PlatformManager" && Login;
    }
}
