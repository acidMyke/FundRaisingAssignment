using System.ComponentModel.DataAnnotations;

namespace FundRaisingAssignment.Application.Models
{
    public enum CampaignStatus
    {
        Draft = 0,
        [Display(Name = "Pending Review")]
        PendingReview = 1,
        Active = 2,
        Inactive = 3,
        Paused = 4,
        Suspended = 5,
        Completed = 6,
        Cancelled = 7,
        [Display(Name = "Flagged")]
        Flagged = 8
    }
}
