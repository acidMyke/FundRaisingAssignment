using System.ComponentModel.DataAnnotations;

namespace FundRaisingAssignment.Application.Models
{
    public enum CampaignCategory
    {
        [Display(Name = "Education")]       Education   = 0,
        [Display(Name = "Medical")]         Medical     = 1,
        [Display(Name = "Environment")]     Environment = 2,
        [Display(Name = "Community")]       Community   = 3,
        [Display(Name = "Technology")]      Technology  = 4,
        [Display(Name = "Arts & Culture")]  Arts        = 5,
        [Display(Name = "Other")]           Other       = 6
    }
}
