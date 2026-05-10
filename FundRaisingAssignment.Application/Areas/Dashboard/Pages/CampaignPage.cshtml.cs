using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FundRaisingAssignment.Application.Boundaries;
using FundRaisingAssignment.Application.Models;
using FundRaisingAssignment.Application.Services;

namespace FundRaisingAssignment.Application.Areas.Dashboard.Pages;

public class CampaignPageModel : PageModel
{
    private readonly ICampaignService _svc;
    private readonly UserManager<ApplicationUser> _um;

    public CampaignPageModel(ICampaignService svc, UserManager<ApplicationUser> um)
    {
        _svc = svc;
        _um = um;
    }

    public CampaignPageView PageView { get; private set; } = new();
    public IReadOnlyList<CampaignReview> Reviews { get; private set; } = [];
    public IReadOnlyList<Donation> Donations { get; private set; } = [];
    public IReadOnlyList<Donation> TopDonations { get; private set; } = [];
    public bool IsOwner { get; private set; }
    public bool CanReview { get; private set; }
    public bool AlreadyReviewed { get; private set; }
    public bool CanDonate { get; private set; }
    public bool IsAdmin { get; private set; }

    [BindProperty] public ReviewInput Review { get; set; } = new();
    [BindProperty] public DonateInput Donate { get; set; } = new();

    public class ReviewInput
    {
        [Required(ErrorMessage = "Please select a star rating.")]
        [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
        public int Stars { get; set; }

        [StringLength(1000, ErrorMessage = "Comment cannot exceed 1000 characters.")]
        [Display(Name = "Comment (optional)")]
        public string? Comment { get; set; }
    }

    public class DonateInput
    {
        [Required(ErrorMessage = "Donation amount is required.")]
        [Range(0.01, 1_000_000, ErrorMessage = "Donation must be between $0.01 and $1,000,000.")]
        [Display(Name = "Donation Amount (USD)")]
        public decimal Amount { get; set; }

        [StringLength(500, ErrorMessage = "Message cannot exceed 500 characters.")]
        [Display(Name = "Message (optional)")]
        public string? Message { get; set; }

        [Display(Name = "Donate anonymously")]
        public bool IsAnonymous { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        var campaign = await _svc.GetCampaignAsync(id);
        if (campaign is null) { PageView.ShowError("Campaign not found."); return Page(); }

        PageView.ShowGoalAndCountdown(campaign);
        Reviews = await _svc.GetCampaignReviewsAsync(id);
        Donations = await _svc.GetCampaignDonationsAsync(id);
        TopDonations = await _svc.GetTopDonationsAsync(id, 10);

        var user = await _um.GetUserAsync(User);
        IsAdmin = User.IsInRole("Admin");

        if (user is not null)
        {
            IsOwner = campaign.OwnerId == user.Id;
            AlreadyReviewed = await _svc.HasUserReviewedAsync(id, user.Id);
            // Reviews allowed: logged-in, not owner, campaign Active, not reviewed yet
            CanReview = !IsOwner && campaign.Status == CampaignStatus.Active && !AlreadyReviewed;
            // Donations: not owner, campaign Active
            CanDonate = !IsOwner && campaign.AcceptsDonations;

            await _svc.TrackUserViewAsync(campaign, user);
        }
        else
        {
            // Guests can donate to Active campaigns
            CanDonate = campaign.AcceptsDonations;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostDonateAsync(Guid id)
    {
        // The Review form isn't on this submit, but the model binder still binds
        // ReviewInput defaults and reports errors under unprefixed keys (Stars,
        // Comment). Drop everything except the Donate fields and the route id.
        foreach (var key in ModelState.Keys.ToList())
        {
            if (!key.StartsWith("Donate") && key != nameof(id))
                ModelState.Remove(key);
        }

        if (!ModelState.IsValid)
        { await ReloadAsync(id); return Page(); }

        if (Donate.Amount <= 0)
        {
            ModelState.AddModelError($"{nameof(Donate)}.{nameof(Donate.Amount)}",
                "Donation amount must be greater than $0.");
            await ReloadAsync(id); return Page();
        }

        var user = await _um.GetUserAsync(User);
        string email = user?.Email ?? "Guest";

        try
        {
            await _svc.DonateAsync(id, user?.Id, email, Donate.Amount, Donate.Message, Donate.IsAnonymous);
            TempData["DonateSuccess"] = $"Thank you! Your donation of ${Donate.Amount:N2} has been received.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostReviewAsync(Guid id)
    {
        foreach (var key in ModelState.Keys.ToList())
        {
            if (!key.StartsWith("Review") && key != nameof(id))
                ModelState.Remove(key);
        }

        if (!ModelState.IsValid)
        { await ReloadAsync(id); return Page(); }

        var user = await _um.GetUserAsync(User);
        if (user is null) return Challenge();

        await _svc.AddReviewAsync(id, user.Id, user.Email ?? "unknown", Review.Stars, Review.Comment);
        TempData["ReviewSuccess"] = "Thank you! Your review has been submitted.";
        return RedirectToPage(new { id });
    }

    private async Task ReloadAsync(Guid id)
    {
        var campaign = await _svc.GetCampaignAsync(id);
        if (campaign is not null) PageView.ShowGoalAndCountdown(campaign);
        Reviews = await _svc.GetCampaignReviewsAsync(id);
        Donations = await _svc.GetCampaignDonationsAsync(id);
        TopDonations = await _svc.GetTopDonationsAsync(id, 10);
        var user = await _um.GetUserAsync(User);
        if (user is not null)
        {
            IsOwner = campaign?.OwnerId == user.Id;
            AlreadyReviewed = await _svc.HasUserReviewedAsync(id, user.Id);
            CanReview = !IsOwner && campaign?.Status == CampaignStatus.Active && !AlreadyReviewed;
        }
        CanDonate = campaign?.AcceptsDonations == true && !IsOwner;
    }
}
