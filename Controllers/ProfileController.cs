using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using CodeDuelArena.Models;

namespace CodeDuelArena.Controllers
{
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;
        
        public ProfileController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }
        
        [HttpGet]
        public async Task<IActionResult> Index(string username)
        {
            var currentUser = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) username = currentUser ?? "";
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Index", "Home");
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();
            
            var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
            if (settings == null)
            {
                settings = new UserSettings { Username = username };
            }
            
            var league = await _db.UserLeagues.FirstOrDefaultAsync(l => l.Username == username);
            var achievements = await _db.UserAchievements
                .Where(a => a.Username == username)
                .Include(a => a.Achievement)
                .ToListAsync();
            
            ViewBag.Settings = settings;
            ViewBag.League = league;
            ViewBag.Achievements = achievements;
            ViewBag.IsOwnProfile = currentUser == username;
            
            return View(user);
        }
        
        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile avatar)
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Index", "Home");
            
            if (avatar != null && avatar.Length > 0 && avatar.Length < 2 * 1024 * 1024) // Максимум 2MB
            {
                // Проверяем что это картинка
                var contentType = avatar.ContentType.ToLower();
                if (contentType != "image/png" && contentType != "image/jpeg" && contentType != "image/jpg" && contentType != "image/gif")
                {
                    TempData["Error"] = "Only PNG, JPG, GIF allowed.";
                    return RedirectToAction("Index");
                }
                
                using var ms = new MemoryStream();
                await avatar.CopyToAsync(ms);
                var avatarData = ms.ToArray();
                
                var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
                if (settings == null)
                {
                    settings = new UserSettings { Username = username };
                    _db.UserSettings.Add(settings);
                }
                
                settings.AvatarData = avatarData;
                settings.AvatarContentType = contentType;
                settings.AvatarUrl = ""; // Больше не используем URL
                
                await _db.SaveChangesAsync();
                TempData["Message"] = "Avatar updated!";
            }
            else
            {
                TempData["Error"] = "File too large. Maximum 2MB.";
            }
            
            return RedirectToAction("Index");
        }
        
       [HttpGet]
[Route("/Profile/Avatar")]
public async Task<IActionResult> Avatar(string username)
{
    // Если есть загруженный аватар в БД — отдаём его
    if (!string.IsNullOrEmpty(username))
    {
        var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
        if (settings?.AvatarData != null && settings.AvatarData.Length > 0)
        {
            var contentType = string.IsNullOrEmpty(settings.AvatarContentType) ? "image/png" : settings.AvatarContentType;
            return File(settings.AvatarData, contentType);
        }
    }
    
    // Иначе генерируем дефолтный аватар с первой буквой
    var letter = (string.IsNullOrEmpty(username) ? "?" : username[0].ToString()).ToUpper();
    var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"150\" height=\"150\"><rect width=\"150\" height=\"150\" rx=\"20\" fill=\"#dc3545\"/><text x=\"75\" y=\"105\" text-anchor=\"middle\" font-size=\"80\" fill=\"white\" font-family=\"Arial,sans-serif\" font-weight=\"bold\">{letter}</text></svg>";
    return Content(svg, "image/svg+xml");
}
        
        [HttpPost]
        public async Task<IActionResult> ChangeUsername(string newUsername)
        {
            var currentUsername = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(currentUsername))
                return Json(new { success = false, error = "Not authenticated" });
            
            if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Length < 3)
                return Json(new { success = false, error = "Username must be at least 3 characters" });
            
            if (newUsername == currentUsername)
                return Json(new { success = false, error = "This is already your username" });
            
            var exists = await _db.Users.AnyAsync(u => u.Username == newUsername);
            if (exists)
                return Json(new { success = false, error = "Username already taken" });
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
            if (user == null)
                return Json(new { success = false, error = "User not found" });
            
            user.Username = newUsername;
            await _db.SaveChangesAsync();
            
            Response.Cookies.Append("auth_user", newUsername, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = DateTime.Now.AddDays(30)
            });
            
            return Json(new { success = true, newUsername = newUsername });
        }
    }
}