using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Models;
using CodeDuelArena.Data;
using System.Security.Cryptography;
using System.Text;

namespace CodeDuelArena.Controllers
{
    public class AuthController : Controller
    {
        private readonly AppDbContext _db;
        
        public AuthController(AppDbContext db)
        {
            _db = db;
        }
        
        [HttpPost]
        public async Task<IActionResult> Register(string username, string tag, string password, string email, bool rememberMe)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
                    return Json(new { success = false, error = "Username must be at least 3 characters" });
                
                if (string.IsNullOrWhiteSpace(tag) || tag.Length < 3 || tag.Length > 20)
                    return Json(new { success = false, error = "Tag must be 3-20 characters" });
                
                if (!System.Text.RegularExpressions.Regex.IsMatch(tag, @"^[a-zA-Z0-9_]+$"))
                    return Json(new { success = false, error = "Tag can only contain letters, numbers, and underscores" });
                
                if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
                    return Json(new { success = false, error = "Password must be at least 4 characters" });
                
                var usernameExists = await _db.Users.AnyAsync(u => u.Username == username);
                if (usernameExists)
                    return Json(new { success = false, error = "Username already taken" });
                
                var tagExists = await _db.Users.AnyAsync(u => u.Tag == tag);
                if (tagExists)
                    return Json(new { success = false, error = "Tag already taken" });
                
                var user = new UserDb
                {
                    Username = username,
                    Tag = tag,
                    PasswordHash = HashPassword(password),
                    Email = email ?? "",
                    RegisteredAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };
                
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                
                SetCookie(username, rememberMe);
                return Json(new { success = true, username = username, tag = tag, score = 0 });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.InnerException?.Message ?? ex.Message });
            }
        }
        
        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);

            if (user == null)
                return Json(new { success = false, error = "User not found" });

            if (!VerifyPassword(password, user.PasswordHash))
                return Json(new { success = false, error = "Incorrect password" });

            user.LastLogin = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            SetCookie(username, rememberMe);

            if (user.IsAdmin)
            {
                Response.Cookies.Append("admin_auth", "true", new CookieOptions
                {
                    HttpOnly = true,
                    Expires = rememberMe ? DateTime.Now.AddDays(30) : DateTime.Now.AddHours(8),
                    SameSite = SameSiteMode.Lax,
                    Path = "/"
                });
            }

            return Json(new { success = true, username = user.Username, tag = user.Tag, score = user.Score, isAdmin = user.IsAdmin });
        }
        
        [HttpPost]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("auth_user");
            return Json(new { success = true });
        }
        
        [HttpGet]
        public async Task<IActionResult> CheckAuth()
        {
            var username = Request.Cookies["auth_user"];
            if (username != null)
            {
                var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user != null)
                    return Json(new { authenticated = true, username = user.Username, tag = user.Tag, score = user.Score });
            }
            return Json(new { authenticated = false });
        }
        
        private void SetCookie(string username, bool remember)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = remember ? DateTime.Now.AddDays(30) : DateTime.Now.AddHours(8)
            };
            Response.Cookies.Append("auth_user", username, options);
        }
        
        private static string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }
        
        private static bool VerifyPassword(string password, string hash)
        {
            return HashPassword(password) == hash;
        }
    }
}