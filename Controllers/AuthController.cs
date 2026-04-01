using Microsoft.AspNetCore.Mvc;
using CodeDuelArena.Models;

namespace CodeDuelArena.Controllers
{
    public class AuthController : Controller
    {
        private static Dictionary<string, (string Password, string Email, int Score, int Wins, int Losses, List<string> Quests)> _users = new();

        [HttpPost]
        public IActionResult Register([FromBody] RegisterModel model)
        {
            if (_users.ContainsKey(model.Username))
                return Json(new { success = false, error = "Имя пользователя уже занято" });
            
            _users[model.Username] = (model.Password, model.Email ?? "", 0, 0, 0, new List<string>());
            
            SetCookie(model.Username, model.RememberMe);
            return Json(new { success = true, username = model.Username, score = 0 });
        }

        [HttpPost]
        public IActionResult Login([FromBody] LoginModel model)
        {
            if (!_users.TryGetValue(model.Username, out var user) || user.Password != model.Password)
                return Json(new { success = false, error = "Неверный логин или пароль" });
            
            SetCookie(model.Username, model.RememberMe);
            return Json(new { success = true, username = model.Username, score = user.Score });
        }

        [HttpPost]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("auth_user");
            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult CheckAuth()
        {
            var username = Request.Cookies["auth_user"];
            if (username != null && _users.ContainsKey(username))
                return Json(new { authenticated = true, username = username, score = _users[username].Score });
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
    }
}