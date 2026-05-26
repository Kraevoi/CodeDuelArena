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
    
    // Обновляем куку
    Response.Cookies.Append("auth_user", newUsername, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = DateTime.Now.AddDays(30)
    });
    
    return Json(new { success = true, newUsername = newUsername });
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
            
            if (avatar != null && avatar.Length > 0 && avatar.Length < 1024 * 1024)
            {
                var uploadsFolder = Path.Combine(_env.WebRootPath, "avatars");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                
                var fileName = $"{username}_{DateTime.Now.Ticks}{Path.GetExtension(avatar.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);
                
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }
                
                var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
                if (settings == null)
                {
                    settings = new UserSettings { Username = username };
                    _db.UserSettings.Add(settings);
                }
                settings.AvatarUrl = $"/avatars/{fileName}";
                await _db.SaveChangesAsync();
            }
            
            return RedirectToAction("Index");
        }
    }
}