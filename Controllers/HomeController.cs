using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using CodeDuelArena.Models;

namespace CodeDuelArena.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        
        public HomeController(AppDbContext db)
        {
            _db = db;
        }
        
        public IActionResult Index() => View();
        public IActionResult Quests() => View(DataStorage.GetQuests());
        
        public async Task<IActionResult> Leaderboard()
        {
            var users = await _db.Users
                .OrderByDescending(u => u.Score)
                .Take(20)
                .ToListAsync();
            return View(users);
        }

        

        [HttpPost]
        public async Task<IActionResult> ReportBug(string bugText)
        {
            if (!string.IsNullOrWhiteSpace(bugText))
            {
                var username = Request.Cookies["auth_user"] ?? "Аноним";
                
                var complaint = new Complaint
                {
                    UserName = username,
                    Message = bugText,
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };
                
                _db.Complaints.Add(complaint);
                await _db.SaveChangesAsync();
                
                // Логируем действие
                await LogActivity(username, "Отправил жалобу", bugText);
                
                //TempData["ReportMessage"] = "Жалоба отправлена";
            }
            return RedirectToAction("Index");
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
    }
}