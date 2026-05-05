using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using FundRaisingAssignment.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundRaisingAssignment.Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DonationsController(DonationService donationService) : ControllerBase
    {
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

            var userIdRaw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdRaw, out var userId))
                return Unauthorized();

            var input = new MakeDonationInput(
                request.CampaignId,
                request.Amount,
                request.Message,
                request.IsAnonymous);

            var result = await donationService.MakeDonationAsync(userId, input, ct);

            return result switch
            {
                DonationResult.Success s => CreatedAtAction(
                    nameof(GetDonation),
                    new { id = s.Donation.Id },
                    new DonationResponse(
                        s.Donation.Id,
                        s.Campaign.Id,
                        s.Campaign.Title,
                        s.Donation.Amount,
                        s.Donation.Message,
                        s.Donation.IsAnonymous,
                        s.Donation.Status.ToString(),
                        s.Donation.CreatedAt)),

                DonationResult.CampaignNotFound nf =>
                    NotFound(new { error = $"Campaign '{nf.CampaignId}' was not found." }),

                DonationResult.CampaignNotActive na =>
                    Conflict(new { error = $"Campaign is currently '{na.CurrentStatus}' and is not accepting donations." }),

                DonationResult.DeadlinePassed =>
                    Conflict(new { error = "Campaign deadline has passed." }),

                DonationResult.TransactionFailed =>
                    StatusCode(StatusCodes.Status500InternalServerError,
                        new { error = "An unexpected error occurred while processing the donation." }),

                _ => StatusCode(StatusCodes.Status500InternalServerError)
            };
        }

        [HttpGet("{id:guid}")]
        public IActionResult GetDonation(Guid id) => NoContent(); // stub for DN05
    }
}
