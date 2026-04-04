using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Models;
using CodeDuelArena.Data;
using Newtonsoft.Json;
using System.Text;
using System.Security.Cryptography;

namespace CodeDuelArena.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        private static readonly string AdminPasswordHash = HashPassword("admin123");
        
        public AdminController(AppDbContext db)
        {
            _db = db;
        }
        
        [HttpGet]
        public IActionResult Login()
        {
            if (IsAdminLoggedIn())
                return RedirectToAction("Dashboard");
            return View();
        }
        
        [HttpPost]
        public IActionResult Login(string password, bool rememberMe)
        {
            if (VerifyPassword(password, AdminPasswordHash))
            {
                SetAdminCookie(rememberMe);
                AdminLogs.Add("Login", "Admin", "Успешный вход");
                return RedirectToAction("Dashboard");
            }
            ViewBag.Error = "Неверный пароль";
            return View();
        }
        
        [HttpGet]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("admin_auth");
            return RedirectToAction("Login");
        }
        
        [HttpGet]
        public async Task<IActionResult> Dashboard()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            
            var users = await _db.Users.ToListAsync();
            var stats = new 
            {
                TotalUsers = users.Count,
                TotalScore = users.Sum(u => u.Score),
                AverageScore = users.Any() ? users.Average(u => u.Score) : 0,
                TopPlayer = users.OrderByDescending(u => u.Score).FirstOrDefault(),
                ActiveToday = users.Count(u => u.LastLogin.Date == DateTime.Today),
                TotalWins = users.Sum(u => u.Wins),
                TotalLosses = users.Sum(u => u.Losses),
                TotalQuests = DataStorage.GetQuests().Count
            };
            
            ViewBag.Stats = stats;
            ViewBag.Logs = AdminLogs.GetRecent(30);
            return View();
        }
        
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            var users = await _db.Users.OrderByDescending(u => u.Score).ToListAsync();
            return View(users);
        }
        
        [HttpPost]
        public async Task<IActionResult> DeleteUser(int id)
        {
            if (!IsAdminLoggedIn()) return Json(new { success = false });
            
            var user = await _db.Users.FindAsync(id);
            if (user != null)
            {
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
                AdminLogs.Add("DeleteUser", "Admin", $"Удален пользователь {user.Username}");
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }
        
        [HttpGet]
        public IActionResult AddQuest()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            return View(new QuestModel());
        }
        
        [HttpPost]
        public IActionResult AddQuest(QuestModel model)
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            
            var quests = DataStorage.GetQuests();
            model.Id = quests.Any() ? quests.Max(q => q.Id) + 1 : 1;
            quests.Add(model);
            
            var json = JsonConvert.SerializeObject(quests, Formatting.Indented);
            System.IO.File.WriteAllText("quests.json", json);
            
            AdminLogs.Add("AddQuest", "Admin", $"Добавлен квест: {model.Title}");
            TempData["Message"] = "Квест добавлен!";
            return RedirectToAction("Quests");
        }
        
        [HttpGet]
        public IActionResult Quests()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            var quests = DataStorage.GetQuests();
            return View(quests);
        }
        
        [HttpPost]
        public IActionResult DeleteQuest(int id)
        {
            if (!IsAdminLoggedIn()) return Json(new { success = false });
            
            var quests = DataStorage.GetQuests();
            var quest = quests.FirstOrDefault(q => q.Id == id);
            if (quest != null)
            {
                quests.Remove(quest);
                var json = JsonConvert.SerializeObject(quests, Formatting.Indented);
                System.IO.File.WriteAllText("quests.json", json);
                AdminLogs.Add("DeleteQuest", "Admin", $"Удален квест: {quest.Title}");
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }
        
        [HttpGet]
        public IActionResult Logs()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            var logs = AdminLogs.GetAll();
            return View(logs);
        }
        
        [HttpGet]
        public async Task<IActionResult> Statistics()
        {
            if (!IsAdminLoggedIn()) return Json(new { error = "Unauthorized" });
            
            var users = await _db.Users.ToListAsync();
            var stats = new
            {
                UsersByDay = users.GroupBy(u => u.RegisteredAt.Date).Select(g => new { Date = g.Key, Count = g.Count() }).OrderBy(g => g.Date)
            };
            return Json(stats);
        }
        
        [HttpGet]
        public async Task<IActionResult> ExportUsers()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            
            var users = await _db.Users.ToListAsync();
            var csv = "Username,Email,Score,Wins,Losses,RegisteredAt\n";
            csv += string.Join("\n", users.Select(u => $"{u.Username},{u.Email},{u.Score},{u.Wins},{u.Losses},{u.RegisteredAt:yyyy-MM-dd HH:mm:ss}"));
            
            AdminLogs.Add("ExportUsers", "Admin", $"Экспортировано {users.Count} пользователей");
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"users_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
        
        private bool IsAdminLoggedIn()
        {
            return Request.Cookies["admin_auth"] == "true";
        }
        
        private void SetAdminCookie(bool remember)
        {
            Response.Cookies.Append("admin_auth", "true", new CookieOptions
            {
                HttpOnly = true,
                Expires = remember ? DateTime.Now.AddDays(7) : DateTime.Now.AddHours(1)
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