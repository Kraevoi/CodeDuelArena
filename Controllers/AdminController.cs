using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using CodeDuelArena.Models; 
using Newtonsoft.Json;

namespace CodeDuelArena.Controllers
{
    public class AdminController : Controller
    {
        private readonly AppDbContext _db;
        
        public AdminController(AppDbContext db)
        {
            _db = db;
        }
        
        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }
        
        [HttpPost]
        public IActionResult Login(string password, bool rememberMe)
        {
            if (password == "admin123")
            {
                Response.Cookies.Append("admin_auth", "true", new CookieOptions
                {
                    HttpOnly = true,
                    Expires = rememberMe ? DateTime.Now.AddDays(30) : DateTime.Now.AddHours(1)
                });
                return RedirectToAction("Dashboard");
            }
            ViewBag.Error = "Wrong password";
            return View();
        }
        
        [HttpGet]
        public IActionResult Dashboard()
        {
            if (Request.Cookies["admin_auth"] != "true")
                return RedirectToAction("Login");
            return View();
        }
        
        [HttpGet]
        public IActionResult Logout()
        {
            Response.Cookies.Delete("admin_auth");
            return RedirectToAction("Login");
        }
        
        [HttpGet]
        public async Task<IActionResult> Users()
        {
            if (Request.Cookies["admin_auth"] != "true")
                return RedirectToAction("Login");
            var users = await _db.Users.ToListAsync();
            return View(users);
        }
        
        [HttpGet]
        public async Task<IActionResult> Quests()
        {
            if (Request.Cookies["admin_auth"] != "true")
                return RedirectToAction("Login");
            var quests = DataStorage.GetQuests();
            return View(quests);
        }
        
        [HttpGet]
        public IActionResult AddQuest()
        {
            if (Request.Cookies["admin_auth"] != "true")
                return RedirectToAction("Login");
            return View(new QuestModel());
        }
        
        [HttpPost]
        public IActionResult AddQuest(QuestModel model)
        {
            if (Request.Cookies["admin_auth"] != "true")
                return RedirectToAction("Login");
            
            var quests = DataStorage.GetQuests();
            model.Id = quests.Any() ? quests.Max(q => q.Id) + 1 : 1;
            quests.Add(model);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(quests, Newtonsoft.Json.Formatting.Indented);
            System.IO.File.WriteAllText("quests.json", json);
            TempData["Message"] = "Квест добавлен!";
            return RedirectToAction("Quests");
        }
        
        [HttpGet]
        public async Task<IActionResult> Complaints()
        {
            if (Request.Cookies["admin_auth"] != "true")
                return RedirectToAction("Login");
            var complaints = await _db.Complaints.OrderByDescending(c => c.CreatedAt).ToListAsync();
            return View(complaints);
        }
        
        [HttpPost]
        public async Task<IActionResult> MarkRead(int id)
        {
            if (Request.Cookies["admin_auth"] != "true")
                return Json(new { success = false });
            
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
            if (Request.Cookies["admin_auth"] != "true")
                return Json(new { success = false });
            
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
        public async Task<IActionResult> ActivityLogs()
        {
            if (Request.Cookies["admin_auth"] != "true")
                return RedirectToAction("Login");
            var logs = await _db.ActivityLogs.OrderByDescending(l => l.Timestamp).Take(200).ToListAsync();
            return View(logs);
        }
        
        [HttpGet]
        public async Task<IActionResult> Impersonate(int id)
        {
            if (Request.Cookies["admin_auth"] != "true")
                return RedirectToAction("Login");
            
            var user = await _db.Users.FindAsync(id);
            if (user == null)
                return NotFound();
            
            Response.Cookies.Append("original_user", Request.Cookies["auth_user"] ?? "");
            Response.Cookies.Append("auth_user", user.Username);
            Response.Cookies.Delete("admin_auth");
            
            return RedirectToAction("Index", "Home");
        }
        
        [HttpGet]
        public IActionResult ReturnToAdmin()
        {
            var originalUser = Request.Cookies["original_user"];
            if (!string.IsNullOrEmpty(originalUser))
            {
                Response.Cookies.Append("auth_user", originalUser);
                Response.Cookies.Delete("original_user");
            }
            Response.Cookies.Append("admin_auth", "true");
            return RedirectToAction("Dashboard");
        }
    }
}