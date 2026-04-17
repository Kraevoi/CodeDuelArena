using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Models;
using CodeDuelArena.Data;

namespace CodeDuelArena.Controllers
{
    public class MessageController : Controller
    {
        private readonly AppDbContext _db;
        
        public MessageController(AppDbContext db)
        {
            _db = db;
        }
        
        [HttpGet]
        public async Task<IActionResult> Inbox()
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Index", "Home");
            
            var messages = await _db.PrivateMessages
                .Where(m => m.ToUser == username)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();
            
            return View(messages);
        }
        
        [HttpGet]
        public async Task<IActionResult> Sent()
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Index", "Home");
            
            var messages = await _db.PrivateMessages
                .Where(m => m.FromUser == username)
                .OrderByDescending(m => m.SentAt)
                .ToListAsync();
            
            return View(messages);
        }
        
        [HttpGet]
        public IActionResult New()
        {
            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> Send(string toUser, string message)
        {
            var fromUser = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(fromUser)) return Json(new { success = false, error = "Не авторизован" });
            
            var toUserExists = await _db.Users.AnyAsync(u => u.Username == toUser);
            if (!toUserExists) return Json(new { success = false, error = "Пользователь не найден" });
            
            var msg = new PrivateMessage
            {
                FromUser = fromUser,
                ToUser = toUser,
                Message = message,
                SentAt = DateTime.UtcNow,
                IsRead = false
            };
            
            _db.PrivateMessages.Add(msg);
            await _db.SaveChangesAsync();
            
            return Json(new { success = true });
        }
        
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            var username = Request.Cookies["auth_user"];
            var message = await _db.PrivateMessages.FirstOrDefaultAsync(m => m.Id == id && m.ToUser == username);
            if (message != null)
            {
                message.IsRead = true;
                await _db.SaveChangesAsync();
            }
            return Json(new { success = true });
        }
    }
}