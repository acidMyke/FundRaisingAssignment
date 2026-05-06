namespace FundRaisingAssignment.Application.Areas.Campaigns.Pages;

using FundRaisingAssignment.Application.Services;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

public class IndexModel : PageModel
{
    private readonly CampaignService _service;

    public IndexModel(CampaignService service)
    {
        _service = service;
    }

    public List<Campaign> Campaigns { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Category { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Location { get; set; }

    public bool HasSearched { get; set; }

    public async Task OnGetAsync()
    {
        Keyword = Keyword?.Trim();
        Category = Category?.Trim();
        Location = Location?.Trim();

        HasSearched = !string.IsNullOrWhiteSpace(Keyword)
                || !string.IsNullOrWhiteSpace(Category)
                || !string.IsNullOrWhiteSpace(Location);

        if (HasSearched)
        {
            Campaigns = await _service.SearchCampaigns(Keyword, Category, Location);
        }

    }
}