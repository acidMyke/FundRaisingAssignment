using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FundRaisingAssignment.Application.Data;
using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FundRaisingAssignment.Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DonationsController(
        ApplicationDbContext context,
        ILogger<DonationsController> logger) : ControllerBase
    {
        private readonly ApplicationDbContext _context = context;
        private readonly ILogger<DonationsController> _logger = logger;

        // --- Request type (nested) ---
        public class MakeDonationRequest
        {
            [Required]
            public Guid CampaignId { get; set; }

            [Required]
            [Range(0.01, 1_000_000, ErrorMessage = "Amount must be between 0.01 and 1,000,000.")]
            public decimal Amount { get; set; }

            [StringLength(500)]
            public string? Message { get; set; }

            public bool IsAnonymous { get; set; } = false;
        }

        // --- Response shape (nested) ---
        public record DonationResponse(
            Guid Id,
            Guid CampaignId,
            string CampaignTitle,
            decimal Amount,
            string? Message,
            bool IsAnonymous,
            string Status,
            DateTime CreatedAt);

        [HttpPost]
        [ProducesResponseType(typeof(DonationResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> MakeDonation(
            [FromBody] MakeDonationRequest request,
            CancellationToken ct)
        {
            if (!ModelState.IsValid)
                return ValidationProblem(ModelState);

            // 1. Resolve donor from authenticated identity
            var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdRaw, out var userId))
                return Unauthorized();

            // 2. Load campaign
            var campaign = await _context.Campaigns
                .FirstOrDefaultAsync(c => c.Id == request.CampaignId, ct);

            if (campaign is null)
                return NotFound(new { error = $"Campaign '{request.CampaignId}' was not found." });

            // 3. Validate campaign state
            if (campaign.Status != CampaignStatus.Active)
                return Conflict(new { error = $"Campaign is currently '{campaign.Status}' and is not accepting donations." });

            if (campaign.EndDate.HasValue && campaign.EndDate.Value < DateTime.UtcNow)
                return Conflict(new { error = "Campaign deadline has passed." });

            // 4. Atomic insert + balance bump
            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
                var donation = new Donation
                {
                    Id = Guid.NewGuid(),
                    CampaignId = campaign.Id,
                    UserId = userId,
                    Amount = request.Amount,
                    Message = request.Message,
                    IsAnonymous = request.IsAnonymous,
                    Status = DonationStatus.Completed, // simulate successful payment
                    CreatedAt = DateTime.UtcNow
                };

                await _context.Donations.AddAsync(donation, ct);

                campaign.CurrentAmount += request.Amount;

                // Sub-flow 7a: auto-complete on goal reached
                if (campaign.CurrentAmount >= campaign.TargetAmount &&
                    campaign.Status == CampaignStatus.Active)
                {
                    campaign.Status = CampaignStatus.Completed;
                    _logger.LogInformation(
                        "Campaign {CampaignId} reached its goal and was auto-completed.",
                        campaign.Id);
                }

                await _context.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);

                _logger.LogInformation(
                    "Donation {DonationId} of {Amount} recorded for campaign {CampaignId} by donor {DonorId}.",
                    donation.Id, donation.Amount, campaign.Id, userId);

                var response = new DonationResponse(
                    donation.Id,
                    campaign.Id,
                    campaign.Title,
                    donation.Amount,
                    donation.Message,
                    donation.IsAnonymous,
                    donation.Status.ToString(),
                    donation.CreatedAt);

                return CreatedAtAction(nameof(GetDonation), new { id = donation.Id }, response);
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync(ct);
                _logger.LogError(ex,
                    "Failed to process donation for campaign {CampaignId} by user {UserId}.",
                    request.CampaignId, userId);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    new { error = "An unexpected error occurred while processing the donation." });
            }
        }

        [HttpGet("{id:guid}")]
        public IActionResult GetDonation(Guid id) => NoContent(); // stub for DN05
    }
}