using Microsoft.AspNetCore.Mvc;
using CodeDuelArena.Data;

namespace CodeDuelArena.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index() => View();
        public IActionResult Quests() => View(DataStorage.GetQuests());
        public IActionResult Leaderboard() => View(DataStorage.GetUsers().OrderByDescending(u => u.Score).Take(10).ToList());

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