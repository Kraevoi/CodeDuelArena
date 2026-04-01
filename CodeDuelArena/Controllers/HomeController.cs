using Microsoft.AspNetCore.Mvc;
using CodeDuelArena.Data;
using System.Linq;
using System;

namespace CodeDuelArena.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            var users = DataStorage.GetUsers();
            ViewBag.OnlineCount = users.Count;
            return View();
        }

        public IActionResult Quests()
        {
            var quests = DataStorage.GetQuests();
            return View(quests);
        }

        public IActionResult Leaderboard()
        {
            var users = DataStorage.GetUsers().OrderByDescending(u => u.Score).Take(10).ToList();
            return View(users);
        }

        [HttpPost]
        public IActionResult ReportBug(string bugText)
        {
            if (!string.IsNullOrWhiteSpace(bugText))
            {
                System.IO.File.AppendAllText("bugs.txt", $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}: {bugText}\n");
                TempData["ReportMessage"] = "Жалоба отправлена. Спасибо, доносчик.";
            }
            return RedirectToAction("Index");
        }
    }
}