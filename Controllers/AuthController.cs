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
        public async Task<IActionResult> Register(RegisterModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Username) || model.Username.Length < 3)
                return Json(new { success = false, error = "Логин минимум 3 символа" });
            
            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 4)
                return Json(new { success = false, error = "Пароль минимум 4 символа" });
            
            var exists = await _db.Users.AnyAsync(u => u.Username == model.Username);
            if (exists)
                return Json(new { success = false, error = "Имя уже занято" });
            
            if (!string.IsNullOrWhiteSpace(model.Email))
            {
                var emailExists = await _db.Users.AnyAsync(u => u.Email == model.Email);
                if (emailExists)
                    return Json(new { success = false, error = "Email уже используется" });
            }
            
            var user = new UserDb
            {
                Username = model.Username,
                PasswordHash = HashPassword(model.Password),
                Email = model.Email ?? "",
                RegisteredAt = DateTime.Now,
                LastLogin = DateTime.Now
            };
            
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            
            SetCookie(model.Username, model.RememberMe);
            return Json(new { success = true, username = model.Username, score = 0 });
        }
        
        [HttpPost]
        public async Task<IActionResult> Login(LoginModel model)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == model.Username);
            
            if (user == null)
                return Json(new { success = false, error = "Пользователь не найден" });
            
            if (!VerifyPassword(model.Password, user.PasswordHash))
                return Json(new { success = false, error = "Неверный пароль" });
            
            user.LastLogin = DateTime.Now;
            await _db.SaveChangesAsync();
            
            SetCookie(model.Username, model.RememberMe);
            return Json(new { success = true, username = user.Username, score = user.Score });
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