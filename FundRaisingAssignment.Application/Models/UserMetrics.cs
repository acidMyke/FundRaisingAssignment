using System;

namespace FundRaisingAssignment.Application.Models
{
    public class UserMetrics
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public int DonationCount { get; set; }
        public int UniqueCampaigns { get; set; }
        public decimal HighDonation { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
