using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Mvc;

namespace FundRaisingAssignment.Application.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DonationRecordController : ControllerBase
    {
        // This would be replaced with a database context in a real app
        private static List<Donation> _donationRecords = new List<Donation>();

        [HttpGet("user/{userId}")]
        public ActionResult<IEnumerable<Donation>> GetDonationRecordsForUser(Guid userId)
        {
            var records = _donationRecords.Where(r => r.UserId == userId).ToList();
            if (!records.Any())
                return NotFound("No donation records available.");
            return Ok(records);
        }

        [HttpGet("{id}")]
        public ActionResult<Donation> GetDonationRecord(Guid id)
        {
            var record = _donationRecords.FirstOrDefault(r => r.Id == id);
            if (record == null)
                return NotFound("Donation record not found.");
            return Ok(record);
        }

        // Add more actions as needed (e.g., for generating receipts)
    }
}
