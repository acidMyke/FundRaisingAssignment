using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Services
{
    public class BadgeService
    {
        private readonly ApplicationDbContext _db;
        public BadgeService(ApplicationDbContext db)
        {
            _db = db;
        }

        public async Task<UserBadgeProgress> GetUserBadgesAsync(Guid userId)
        {
            var metrics = await _db.UserMetrics.FirstOrDefaultAsync(m => m.UserId == userId);
            if (metrics == null)
            {
                metrics = new Models.UserMetrics { DonationCount = 0, UniqueCampaigns = 0, HighDonation = 0 };
            }

            var badges = new List<Badge>();
            // 1. Donation Count (🪙)
            BadgeTier? donationCountTier = null;
            if (metrics.DonationCount >= 10) donationCountTier = BadgeTier.Gold;
            else if (metrics.DonationCount >= 5) donationCountTier = BadgeTier.Silver;
            else if (metrics.DonationCount >= 1) donationCountTier = BadgeTier.Bronze;
            badges.Add(new Badge
            {
                Type = BadgeType.DonationCount,
                Name = "Donation Count",
                Description = "Total number of donations made.",
                Tier = donationCountTier,
                Progress = metrics.DonationCount,
                NextTierRequirement = donationCountTier == null ? 1 : donationCountTier == BadgeTier.Bronze ? 5 : donationCountTier == BadgeTier.Silver ? 10 : 0,
                Icon = "🪙",
                BorderColor = donationCountTier == BadgeTier.Gold ? "#ffd700" : donationCountTier == BadgeTier.Silver ? "#c0c0c0" : donationCountTier == BadgeTier.Bronze ? "#cd7f32" : "#e5e7eb"
            });
            // 2. High Donation Count (💰)
            // Count of donations >= $100
            var highDonationCount = await _db.Donations.CountAsync(d => d.UserId == userId && d.Amount >= 100);
            BadgeTier? highDonationCountTier = null;
            if (highDonationCount >= 10) highDonationCountTier = BadgeTier.Gold;
            else if (highDonationCount >= 5) highDonationCountTier = BadgeTier.Silver;
            else if (highDonationCount >= 1) highDonationCountTier = BadgeTier.Bronze;
            badges.Add(new Badge
            {
                Type = BadgeType.HighDonationCount,
                Name = "High Donation Count",
                Description = "Number of donations of $100 or more.",
                Tier = highDonationCountTier,
                Progress = highDonationCount,
                NextTierRequirement = highDonationCountTier == null ? 1 : highDonationCountTier == BadgeTier.Bronze ? 5 : highDonationCountTier == BadgeTier.Silver ? 10 : 0,
                Icon = "💰",
                BorderColor = highDonationCountTier == BadgeTier.Gold ? "#ffd700" : highDonationCountTier == BadgeTier.Silver ? "#c0c0c0" : highDonationCountTier == BadgeTier.Bronze ? "#cd7f32" : "#e5e7eb"
            });
            // 3. Huge Supporter (🏆)
            // Highest total to a single campaign
            // EF Core cannot translate DefaultIfEmpty(0) in this context, so materialize first
            var supportSums = await _db.Donations
                .Where(d => d.UserId == userId)
                .GroupBy(d => d.CampaignId)
                .Select(g => g.Sum(d => d.Amount))
                .ToListAsync();
            var maxSupport = supportSums.Count > 0 ? supportSums.Max() : 0;
            BadgeTier? hugeSupporterTier = null;
            if (maxSupport >= 1000) hugeSupporterTier = BadgeTier.Gold;
            else if (maxSupport >= 500) hugeSupporterTier = BadgeTier.Silver;
            else if (maxSupport >= 100) hugeSupporterTier = BadgeTier.Bronze;
            badges.Add(new Badge
            {
                Type = BadgeType.HugeSupporter,
                Name = "Huge Supporter",
                Description = "Highest total donated to a single campaign.",
                Tier = hugeSupporterTier,
                Progress = (int)maxSupport,
                NextTierRequirement = hugeSupporterTier == null ? 100 : hugeSupporterTier == BadgeTier.Bronze ? 500 : hugeSupporterTier == BadgeTier.Silver ? 1000 : 0,
                Icon = "🏆",
                BorderColor = hugeSupporterTier == BadgeTier.Gold ? "#ffd700" : hugeSupporterTier == BadgeTier.Silver ? "#c0c0c0" : hugeSupporterTier == BadgeTier.Bronze ? "#cd7f32" : "#e5e7eb"
            });
            // 4. Big Heart (💖)
            BadgeTier? bigHeartTier = null;
            if (metrics.UniqueCampaigns >= 10) bigHeartTier = BadgeTier.Gold;
            else if (metrics.UniqueCampaigns >= 5) bigHeartTier = BadgeTier.Silver;
            else if (metrics.UniqueCampaigns >= 1) bigHeartTier = BadgeTier.Bronze;
            badges.Add(new Badge
            {
                Type = BadgeType.BigHeart,
                Name = "Big Heart",
                Description = "Number of unique campaigns donated to.",
                Tier = bigHeartTier,
                Progress = metrics.UniqueCampaigns,
                NextTierRequirement = bigHeartTier == null ? 1 : bigHeartTier == BadgeTier.Bronze ? 5 : bigHeartTier == BadgeTier.Silver ? 10 : 0,
                Icon = "💖",
                BorderColor = bigHeartTier == BadgeTier.Gold ? "#ffd700" : bigHeartTier == BadgeTier.Silver ? "#c0c0c0" : bigHeartTier == BadgeTier.Bronze ? "#cd7f32" : "#e5e7eb"
            });
            // 5. First Donation (🎉)
            BadgeTier? firstDonationTier = null;
            if (metrics.DonationCount >= 1) firstDonationTier = BadgeTier.Special;
            badges.Add(new Badge
            {
                Type = BadgeType.FirstDonation,
                Name = "First Donation",
                Description = "Awarded for making your first donation!",
                Tier = firstDonationTier,
                Progress = metrics.DonationCount >= 1 ? 1 : 0,
                NextTierRequirement = 1,
                Icon = "🎉",
                BorderColor = firstDonationTier == BadgeTier.Special ? "#6366f1" : "#e5e7eb"
            });
            return new UserBadgeProgress { UserId = userId, Badges = badges };
        }

        // Call this after a donation is made to update metrics
        public async Task UpdateUserMetricsAsync(Guid userId, Guid campaignId, decimal amount)
        {
            var metrics = await _db.UserMetrics.FirstOrDefaultAsync(m => m.UserId == userId);
            var now = DateTime.UtcNow;
            if (metrics == null)
            {
                metrics = new Models.UserMetrics
                {
                    UserId = userId,
                    DonationCount = 1,
                    UniqueCampaigns = 1,
                    HighDonation = amount,
                    LastUpdated = now
                };
                _db.UserMetrics.Add(metrics);
            }
            else
            {
                metrics.DonationCount += 1;
                // Check if this campaign is new for the user
                var donatedCampaignIds = await _db.Donations
                    .Where(d => d.UserId == userId)
                    .Select(d => d.CampaignId)
                    .Distinct()
                    .ToListAsync();
                if (!donatedCampaignIds.Contains(campaignId))
                {
                    metrics.UniqueCampaigns += 1;
                }
                if (amount > metrics.HighDonation)
                {
                    metrics.HighDonation = amount;
                }
                metrics.LastUpdated = now;
            }
            await _db.SaveChangesAsync();
        }
    }
    
}
