using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;

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
        public IActionResult ReportBug(string bugText)
        {
            if (!string.IsNullOrWhiteSpace(bugText))
                System.IO.File.AppendAllText("bugs.txt", $"{DateTime.Now}: {bugText}\n");
            TempData["ReportMessage"] = "Жалоба отправлена";
            return RedirectToAction("Index");
        }
    }
}