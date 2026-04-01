using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using CodeDuelArena.Models;
using CodeDuelArena.Data;
using System.Text.Json;

namespace CodeDuelArena.Controllers
{
    public class AuthController : Controller
    {
        [HttpPost]
        public IActionResult Register(RegisterModel model)
        {
            if (ModelState.IsValid)
            {
                if (UserAccounts.Register(model.Username, model.Password, model.Email, out string error))
                {
                    var account = UserAccounts.Login(model.Username, model.Password, out _);
                    if (account != null)
                    {
                        SetAuthCookie(account.Username, model.RememberMe);
                        return Json(new { success = true, username = account.Username });
                    }
                }
                return Json(new { success = false, error = error });
            }
            return Json(new { success = false, error = "Неверные данные" });
        }
        
        [HttpPost]
        public IActionResult Login(LoginModel model)
        {
            var account = UserAccounts.Login(model.Username, model.Password, out string error);
            if (account != null)
            {
                SetAuthCookie(account.Username, model.RememberMe);
                return Json(new { success = true, username = account.Username, score = account.Score });
            }
            return Json(new { success = false, error = error });
        }
        
        [HttpPost]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("auth_user");
            return Json(new { success = true });
        }
        
        public IActionResult CheckAuth()
        {
            var username = GetAuthUser();
            if (username != null)
            {
                var account = UserAccounts.GetByUsername(username);
                if (account != null)
                {
                    return Json(new { authenticated = true, username = account.Username, score = account.Score });
                }
            }
            return Json(new { authenticated = false });
        }
        
        private void SetAuthCookie(string username, bool rememberMe)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = rememberMe ? DateTime.Now.AddDays(30) : DateTime.Now.AddHours(8)
            };
            Response.Cookies.Append("auth_user", username, options);
        }
        
        private string? GetAuthUser()
        {
            return Request.Cookies["auth_user"];
        }
    }
}