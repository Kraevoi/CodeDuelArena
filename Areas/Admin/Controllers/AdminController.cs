using Microsoft.AspNetCore.Mvc;

namespace CodeDuelArena.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class AdminController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToPage("/Login");
        }
    }
}