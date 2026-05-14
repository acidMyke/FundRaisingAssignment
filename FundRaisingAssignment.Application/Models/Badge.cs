using System;
using System.Collections.Generic;

namespace FundRaisingAssignment.Application.Models
{
    public enum BadgeType
    {
        DonationCount,        // Total donations (🪙)
        HighDonationCount,    // # of $100+ donations (💰)
        HugeSupporter,        // Highest total to a single campaign (🏆)
        BigHeart,             // Unique campaigns (💖)
        FirstDonation         // First donation (🎉)
    }

    public enum BadgeTier
    {
        Bronze,  // #cd7f32
        Silver,  // #c0c0c0
        Gold,    // #ffd700
        Special  // For one-time badges (e.g. First Donation)
    }

    public class Badge
    {
        public BadgeType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public BadgeTier? Tier { get; set; }
        public int Progress { get; set; } // e.g. 12 donations
        public int NextTierRequirement { get; set; } // e.g. 15 for next tier
        public string Icon { get; set; } = string.Empty; // Icon for badge
        public string BorderColor { get; set; } = string.Empty; // Hex color for tier border
    }

    public class UserBadgeProgress
    {
        public Guid UserId { get; set; }
        public List<Badge> Badges { get; set; } = new();
    }
}
