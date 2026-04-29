using System;

namespace FundRaisingAssignment.Application.Models
{
    public class DonationRecord
    {
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public int Id { get; set; }
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public Guid DoneeId { get; set; } // Foreign key to ApplicationUser
        [System.ComponentModel.DataAnnotations.Required]
        public decimal Amount { get; set; }
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public DateTime Date { get; set; }
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public string? ReceiptNumber { get; set; }
        [System.ComponentModel.DataAnnotations.Required]
        public string PaymentMethod { get; set; }
        public string? Notes { get; set; }

        // Foreign key to Campaign
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public Guid CampaignId { get; set; }
        [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
        public Campaign? Campaign { get; set; }
    }
}
