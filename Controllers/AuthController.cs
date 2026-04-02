using Microsoft.AspNetCore.Mvc;
using CodeDuelArena.Models;

namespace CodeDuelArena.Controllers
{
    public class AuthController : Controller
    {
        private static Dictionary<string, (string Password, string Email, int Score, int Wins, int Losses, List<string> Quests)> _users = new();

        [HttpPost]
        public IActionResult Register(RegisterModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Username) || model.Username.Length < 3)
                return Json(new { success = false, error = "Логин минимум 3 символа" });
            
            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 4)
                return Json(new { success = false, error = "Пароль минимум 4 символа" });
            
            if (_users.ContainsKey(model.Username))
                return Json(new { success = false, error = "Имя уже занято" });
            
            _users[model.Username] = (model.Password, model.Email ?? "", 0, 0, 0, new List<string>());
            
            SetCookie(model.Username, model.RememberMe);
            return Json(new { success = true, username = model.Username, score = 0 });
        }

        [HttpPost]
        public IActionResult Login(LoginModel model)
        {
            if (!_users.TryGetValue(model.Username, out var user))
                return Json(new { success = false, error = "Пользователь не найден" });
            
            if (user.Password != model.Password)
                return Json(new { success = false, error = "Неверный пароль" });
            
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
                return Json(new { authenticated = true, username, score = _users[username].Score });
            return Json(new { authenticated = false });
        }
        
        public static int GetUserScore(string username)
        {
            return _users.TryGetValue(username, out var user) ? user.Score : 0;
        }
        
        public static void AddUserScore(string username, int points)
        {
            if (_users.ContainsKey(username))
            {
                var user = _users[username];
                _users[username] = (user.Password, user.Email, user.Score + points, user.Wins, user.Losses, user.Quests);
            }
        }
        
        public static void AddUserWin(string username)
        {
            if (_users.ContainsKey(username))
            {
                var user = _users[username];
                _users[username] = (user.Password, user.Email, user.Score + 100, user.Wins + 1, user.Losses, user.Quests);
            }
        }
        
        public static void AddQuestToUser(string username, string questId)
        {
            if (_users.ContainsKey(username))
            {
                var user = _users[username];
                if (!user.Quests.Contains(questId))
                {
                    var newQuests = new List<string>(user.Quests) { questId };
                    _users[username] = (user.Password, user.Email, user.Score, user.Wins, user.Losses, newQuests);
                }
            }
        }
        
        public static bool HasCompletedQuest(string username, string questId)
        {
            return _users.TryGetValue(username, out var user) && user.Quests.Contains(questId);
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