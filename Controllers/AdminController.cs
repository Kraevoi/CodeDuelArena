using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using System.Security.Cryptography;
using System.Text;

namespace CodeDuelArena.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private static readonly string AdminPasswordHash = HashPassword("admin123");
        
        public AdminController(AppDbContext db)
        {
            _db = db;
        }
        
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        
        [HttpPost]
        public IActionResult Login(string password)
        {
            if (HashPassword(password) == AdminPasswordHash)
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
            if (Request.Cookies["admin_auth"] != "true") return RedirectToAction("Login");
            return View();
        }
        
        [HttpGet]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("admin_auth");
            return RedirectToAction("Login");
        }
        
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
    }
}