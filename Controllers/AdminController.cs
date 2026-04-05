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
    public async Task<IActionResult> DuelTasks()
{
    if (!IsAdminLoggedIn()) return RedirectToAction("Login");
    var tasks = await _db.DuelTasks.OrderByDescending(t => t.CreatedAt).ToListAsync();
    return View(tasks);
}

    [HttpGet]
    public IActionResult AddDuelTask()
    {
        if (!IsAdminLoggedIn()) return RedirectToAction("Login");
        return View(new DuelTask());
    }

    [HttpPost]
    public async Task<IActionResult> AddDuelTask(DuelTask model)
    {
        if (!IsAdminLoggedIn()) return RedirectToAction("Login");
    
        model.CreatedAt = DateTime.UtcNow;
        _db.DuelTasks.Add(model);
        await _db.SaveChangesAsync();
    
        await LogActivity("Admin", "Добавил задание для дуэли", $"{model.Title}");
        TempData["Message"] = "Задание для дуэли добавлено!";
        return RedirectToAction("DuelTasks");
    }

    [HttpPost]
    public async Task<IActionResult> ToggleDuelTask(int id)
    {
        if (!IsAdminLoggedIn()) return Json(new { success = false });

        var task = await _db.DuelTasks.FindAsync(id);
        if (task != null)
        {
            task.IsActive = !task.IsActive;
            await _db.SaveChangesAsync();
            await LogActivity("Admin", task.IsActive ? "Активировал задание дуэли" : "Деактивировал задание дуэли", task.Title);
            return Json(new { success = true, isActive = task.IsActive });
        }   
    return Json(new { success = false });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDuelTask(int id)
    {
        if (!IsAdminLoggedIn()) return Json(new { success = false });

        var task = await _db.DuelTasks.FindAsync(id);
        if (task != null)
        {
            _db.DuelTasks.Remove(task);
            await _db.SaveChangesAsync();
            await LogActivity("Admin", "Удалил задание дуэли", task.Title);
            return Json(new { success = true });
        }
    return Json(new { success = false });
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
            var unreadComplaints = await _db.Complaints.CountAsync(c => !c.IsRead);
            var stats = new 
            {
                TotalUsers = users.Count,
                TotalScore = users.Sum(u => u.Score),
                AverageScore = users.Any() ? users.Average(u => u.Score) : 0,
                TopPlayer = users.OrderByDescending(u => u.Score).FirstOrDefault(),
                ActiveToday = users.Count(u => u.LastLogin.Date == DateTime.UtcNow.Date),
                TotalWins = users.Sum(u => u.Wins),
                TotalLosses = users.Sum(u => u.Losses),
                TotalQuests = DataStorage.GetQuests().Count,
                UnreadComplaints = unreadComplaints
            };
            
            ViewBag.Stats = stats;
            return View();
        }
        
        [HttpGet]
        public async Task<IActionResult> Complaints()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            
            var complaints = await _db.Complaints
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(complaints);
        }
        
        [HttpPost]
        public async Task<IActionResult> MarkComplaintRead(int id)
        {
            if (!IsAdminLoggedIn()) return Json(new { success = false });
            
            var complaint = await _db.Complaints.FindAsync(id);
            if (complaint != null)
            {
                complaint.IsRead = true;
                await _db.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }
        
        [HttpPost]
        public async Task<IActionResult> DeleteComplaint(int id)
        {
            if (!IsAdminLoggedIn()) return Json(new { success = false });
            
            var complaint = await _db.Complaints.FindAsync(id);
            if (complaint != null)
            {
                _db.Complaints.Remove(complaint);
                await _db.SaveChangesAsync();
                return Json(new { success = true });
            }
            return Json(new { success = false });
        }
        
        [HttpGet]
        public async Task<IActionResult> GetUnreadCount()
        {
            if (!IsAdminLoggedIn()) return Json(new { count = 0 });
            
            var count = await _db.Complaints.CountAsync(c => !c.IsRead);
            return Json(new { count = count });
        }
        

        [HttpGet]
        public async Task<IActionResult> ActivityLogs()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            
            var logs = await _db.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .Take(200)
                .ToListAsync();
            return View(logs);
        }
        
        [HttpGet]
        public async Task<IActionResult> AllLogs()
        {
            if (!IsAdminLoggedIn()) return RedirectToAction("Login");
            
            var logs = await _db.ActivityLogs
                .OrderByDescending(l => l.Timestamp)
                .ToListAsync();
            return View(logs);
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
                await LogActivity(user.Username, "Админ удалил пользователя", $"Удален пользователь {user.Username}");
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
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
                return Json(new { success = true });
            }
            return Json(new { success = false });
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
            
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", $"users_export_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }
        
        private async Task LogActivity(string userName, string action, string details = "")
        {
            var log = new ActivityLog
            {
                UserName = userName,
                Action = action,
                Details = details,
                Timestamp = DateTime.UtcNow,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"
            };
            _db.ActivityLogs.Add(log);
            await _db.SaveChangesAsync();
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