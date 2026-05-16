using FundRaisingAssignment.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundRaisingAssignment.Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CampaignDigestController(ICampaignDigestService digestService) : ControllerBase
    {
        [HttpPost("trigger")]
        [Authorize(Roles = "Admin")] // Require Admin to trigger digests
        public async Task<IActionResult> TriggerDigest()
        {
            try
            {
                var batchId = await digestService.ValidateAndEnqueueAsync();
                return Ok(new { message = "Digest processing started successfully.", BatchId = batchId });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while processing digests.", error = ex.Message });
            }
        }
    }
}
