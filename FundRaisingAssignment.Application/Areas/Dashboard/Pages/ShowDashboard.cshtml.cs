using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages
{
    [Authorize(Roles = "PlatformManager")]
    public class ShowDashboardModel : PageModel
    {
        private readonly DashboardService _dashboardService;
        private readonly AnalyticsService _analyticsService;

        public ShowDashboardModel(DashboardService dashboardService, AnalyticsService analyticsService)
        {
            _dashboardService = dashboardService;
            _analyticsService = analyticsService;
        }

        [BindProperty(SupportsGet = true)]
        public DashboardFilter Filters { get; set; } = new DashboardFilter();

        public DashboardViewModel ViewModel { get; set; } = new DashboardViewModel();

        public void OnGet()
        {
            if (Filters.IsValid())
            {
                var data = _dashboardService.GetDashboardData("Platform", Filters);
                var issues = _analyticsService.DetectIssues(Filters);

                ViewModel = new DashboardViewModel
                {
                    Filters = Filters,
                    KPIs = data.KPIs,
                    Trends = data.Trends,
                    Issues = issues
                };
            }
        }
    }
}
