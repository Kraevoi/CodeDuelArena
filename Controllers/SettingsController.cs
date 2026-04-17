using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using CodeDuelArena.Models;

namespace CodeDuelArena.Controllers
{
    public class SettingsController : Controller
    {
        private readonly AppDbContext _db;
        
        public SettingsController(AppDbContext db)
        {
            _db = db;
        }
        
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Index", "Home");
            
            var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
            if (settings == null)
            {
                settings = new UserSettings { Username = username };
                _db.UserSettings.Add(settings);
                await _db.SaveChangesAsync();
            }
            
            return View(settings);
        }
        
        [HttpPost]
        public async Task<IActionResult> UpdateTheme(string theme)
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return Json(new { success = false });
            
            var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
            if (settings == null)
            {
                settings = new UserSettings { Username = username };
                _db.UserSettings.Add(settings);
            }
            settings.Theme = theme;
            await _db.SaveChangesAsync();
            
            Response.Cookies.Append("user_theme", theme, new CookieOptions 
            { 
                Expires = DateTime.Now.AddYears(1),
                Path = "/",
                SameSite = SameSiteMode.Lax
            });
            
            return Json(new { success = true });
        }
        
        [HttpPost]
        public async Task<IActionResult> UpdateCustomCss(string customCss)
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return Json(new { success = false });
            
            var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
            if (settings == null)
            {
                settings = new UserSettings { Username = username };
                _db.UserSettings.Add(settings);
            }
            settings.CustomCss = customCss;
            await _db.SaveChangesAsync();
            
            return Json(new { success = true });
        }
        
        [HttpGet]
        public async Task<IActionResult> GetTheme()
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return Json(new { theme = "dark" });
            
            var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
            var theme = settings?.Theme ?? "dark";
            
            return Json(new { theme = theme });
        }
    }
}