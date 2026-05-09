using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FundRaisingAssignment.Application.Models
{
    /// <summary>
    /// Entity representing a notification sent to a fundraiser after campaign review.
    /// Maps to «entity» FundRaiserNotification in BCE Diagram 2.
    /// </summary>
    [Table("FundRaiserNotifications")]
    public class FundRaiserNotification
    {
        /// <summary>notificationId : string</summary>
        public Guid NotificationId { get; set; } = Guid.NewGuid();

        /// <summary>The campaign this notification is about.</summary>
        public Guid CampaignId { get; set; }
        public Campaign? Campaign { get; set; }

        /// <summary>reviewOutcome : string – e.g. "Approved" or "Removed: spam content"</summary>
        [Required]
        [StringLength(1000)]
        public string ReviewOutcome { get; set; } = string.Empty;

        public DateTime SentAt { get; set; } = DateTime.UtcNow;

        public bool IsRead { get; set; } = false;

        /// <summary>
        /// sendReviewNotification(reviewOutcome) – populates the outcome field.
        /// Maps to FundRaiserNotification.sendReviewNotification() in BCE Diagram 2.
        /// </summary>
        public void SendReviewNotification(string reviewOutcome)
        {
            ReviewOutcome = reviewOutcome;
            SentAt = DateTime.UtcNow;
        }
    }
}
