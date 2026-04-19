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
        public async Task<IActionResult> Register(string username, string password, string email, bool rememberMe)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
                    return Json(new { success = false, error = "Логин минимум 3 символа" });
                
                if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
                    return Json(new { success = false, error = "Пароль минимум 4 символа" });
                
                var exists = await _db.Users.AnyAsync(u => u.Username == username);
                if (exists)
                    return Json(new { success = false, error = "Имя уже занято" });
                
                var user = new UserDb
                {
                    Username = username,
                    PasswordHash = HashPassword(password),
                    Email = email ?? "",
                    RegisteredAt = DateTime.UtcNow,
                    LastLogin = DateTime.UtcNow
                };
                
                _db.Users.Add(user);
                await _db.SaveChangesAsync();
                
                SetCookie(username, rememberMe);
                return Json(new { success = true, username = username, score = 0 });
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
        return Json(new { success = false, error = "Пользователь не найден" });
    
    if (!VerifyPassword(password, user.PasswordHash))
        return Json(new { success = false, error = "Неверный пароль" });
    
    user.LastLogin = DateTime.UtcNow;
    await _db.SaveChangesAsync();
    
    SetCookie(username, rememberMe);
    return Json(new { success = true, username = user.Username, score = user.Score }); // ← БЕЗ СООБЩЕНИЯ
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
                    return Json(new { authenticated = true, username = user.Username, score = user.Score });
            }
            return Json(new { authenticated = false });
        }
        
        private void SetCookie(string username, bool remember)
        {
            Response.Cookies.Append("auth_user", username, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Expires = remember ? DateTime.Now.AddDays(30) : DateTime.Now.AddHours(8)
            });
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