using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using CodeDuelArena.Models;
using CodeDuelArena.Data;
using System.Text.Json;

namespace CodeDuelArena.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : Controller
    {
        [HttpPost]
        [Route("register")]
        [IgnoreAntiforgeryToken]
        public IActionResult Register([FromBody] RegisterModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                return Json(new { success = false, error = "Заполните все поля" });
            }

            if (UserAccounts.Register(model.Username, model.Password, model.Email ?? "", out string error))
            {
                var account = UserAccounts.Login(model.Username, model.Password, out _);
                if (account != null)
                {
                    SetAuthCookie(account.Username, model.RememberMe);
                    
                    var users = DataStorage.GetUsers();
                    if (!users.Any(u => u.Username == account.Username))
                    {
                        users.Add(new UserModel 
                        { 
                            Username = account.Username,
                            Score = account.Score,
                            Wins = account.Wins,
                            Losses = account.Losses,
                            CompletedQuests = account.CompletedQuests ?? new List<string>()
                        });
                        DataStorage.SaveUsers(users);
                    }
                    
                    return Json(new { success = true, username = account.Username, score = account.Score });
                }
                return Json(new { success = false, error = "Ошибка при входе после регистрации" });
            }
            return Json(new { success = false, error = error });
        }
        
        [HttpPost]
        [Route("login")]
        [IgnoreAntiforgeryToken]
        public IActionResult Login([FromBody] LoginModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
            {
                return Json(new { success = false, error = "Заполните все поля" });
            }

            var account = UserAccounts.Login(model.Username, model.Password, out string error);
            if (account != null)
            {
                SetAuthCookie(account.Username, model.RememberMe);
                
                var users = DataStorage.GetUsers();
                var existingUser = users.FirstOrDefault(u => u.Username == account.Username);
                if (existingUser == null)
                {
                    users.Add(new UserModel 
                    { 
                        Username = account.Username,
                        Score = account.Score,
                        Wins = account.Wins,
                        Losses = account.Losses,
                        CompletedQuests = account.CompletedQuests ?? new List<string>()
                    });
                    DataStorage.SaveUsers(users);
                }
                else
                {
                    existingUser.Score = account.Score;
                    existingUser.Wins = account.Wins;
                    existingUser.Losses = account.Losses;
                    existingUser.CompletedQuests = account.CompletedQuests ?? new List<string>();
                    DataStorage.SaveUsers(users);
                }
                
                return Json(new { success = true, username = account.Username, score = account.Score });
            }
            return Json(new { success = false, error = error });
        }
        
        [HttpPost]
        [Route("logout")]
        [IgnoreAntiforgeryToken]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("auth_user");
            return Json(new { success = true });
        }
        
        [HttpGet]
        [Route("check")]
        [IgnoreAntiforgeryToken]
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