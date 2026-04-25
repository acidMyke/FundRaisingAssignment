using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundRaisingAssignment.Application.Controllers
{
    [Authorize(Policy = "RequireThreeDaysJoined")]
    public class CampaignController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(Models.Campaign model)
        {
            if (ModelState.IsValid)
            {
                // Logic to save campaign would go here.
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }
    }
}
