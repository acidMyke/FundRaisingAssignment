using FundRaisingAssignment.Application.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FundRaisingAssignment.Application.Controllers
{
    [Authorize(Roles = ApplicationRole.Names.Admin)]
    public class InternalPortalController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
