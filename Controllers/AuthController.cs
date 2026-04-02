using Microsoft.AspNetCore.Mvc;
using CodeDuelArena.Models;
using CodeDuelArena.Data;

namespace CodeDuelArena.Controllers
{
    public class AuthController : Controller
    {
        [HttpPost]
        public IActionResult Register(RegisterModel model)
        {
            if (UserAccounts.Register(model.Username, model.Password, model.Email ?? "", out string error))
            {
                SetCookie(model.Username, model.RememberMe);
                return Json(new { success = true, username = model.Username, score = 0 });
            }
            return Json(new { success = false, error = error });
        }

        [HttpPost]
        public IActionResult Login(LoginModel model)
        {
            var account = UserAccounts.Login(model.Username, model.Password, out string error);
            if (account != null)
            {
                SetCookie(model.Username, model.RememberMe);
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

        [HttpGet]
        public IActionResult CheckAuth()
        {
            var username = Request.Cookies["auth_user"];
            if (username != null)
            {
                var account = UserAccounts.GetByUsername(username);
                if (account != null)
                    return Json(new { authenticated = true, username = account.Username, score = account.Score });
            }
            return Json(new { authenticated = false });
        }

        [HttpGet]
        public IActionResult GetAllUsers()
        {
            var users = UserAccounts.GetAllUsers();
            return Json(users.Select(u => new { u.Username, u.Score, u.Wins, u.Losses, u.CompletedQuests }));
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