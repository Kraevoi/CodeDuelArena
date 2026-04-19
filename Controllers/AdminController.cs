using Microsoft.AspNetCore.Mvc;

namespace CodeDuelArena.Controllers
{
    public class AdminController : Controller
    {
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        
        [HttpPost]
        public IActionResult Login(string password)
        {
            if (password == "admin123")
            {
                Response.Cookies.Append("admin_auth", "true");
                return RedirectToAction("Dashboard");
            }
            ViewBag.Error = "Wrong password";
            return View();
        }
        
        [HttpGet]
        public IActionResult Dashboard()
        {
            if (Request.Cookies["admin_auth"] == "true")
            {
                return View();
            }
            return RedirectToAction("Login");
        }
        
        [HttpGet]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("admin_auth");
            return RedirectToAction("Login");
        }
    }
}