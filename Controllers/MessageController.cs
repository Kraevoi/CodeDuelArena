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
        
        // Главная страница чатов
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Index", "Home");
            return View();
        }
        
        // Получить список диалогов
        [HttpGet]
        public async Task<IActionResult> GetChats()
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return Json(new List<object>());
            
            var sent = await _db.PrivateMessages
                .Where(m => m.FromUser == username)
                .Select(m => m.ToUser)
                .Distinct()
                .ToListAsync();
            
            var received = await _db.PrivateMessages
                .Where(m => m.ToUser == username)
                .Select(m => m.FromUser)
                .Distinct()
                .ToListAsync();
            
            var allPartners = sent.Union(received).Distinct();
            
            var chats = new List<object>();
            foreach (var partner in allPartners)
            {
                var lastMsg = await _db.PrivateMessages
                    .Where(m => (m.FromUser == username && m.ToUser == partner) || 
                                (m.FromUser == partner && m.ToUser == username))
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();
                
                var unread = await _db.PrivateMessages
                    .CountAsync(m => m.FromUser == partner && m.ToUser == username && !m.IsRead);
                
                var partnerUser = await _db.Users.FirstOrDefaultAsync(u => u.Username == partner);
                
                chats.Add(new {
                    username = partner,
                    tag = partnerUser?.Tag ?? "",
                    isAdmin = partnerUser?.IsAdmin ?? false,
                    lastMessage = lastMsg?.Message ?? "",
                    lastTime = lastMsg?.SentAt.ToString("HH:mm") ?? "",
                    unread = unread
                });
            }
            
            return Json(chats.OrderByDescending(c => ((dynamic)c).lastTime));
        }
        
        // Получить историю сообщений с пользователем
        [HttpGet]
        public async Task<IActionResult> GetMessages(string withUser, int skip = 0, int take = 50)
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return Json(new List<object>());
            
            var messages = await _db.PrivateMessages
                .Where(m => (m.FromUser == username && m.ToUser == withUser) || 
                            (m.FromUser == withUser && m.ToUser == username))
                .OrderByDescending(m => m.SentAt)
                .Skip(skip)
                .Take(take)
                .ToListAsync();
            
            // Отмечаем как прочитанные
            var unread = await _db.PrivateMessages
                .Where(m => m.FromUser == withUser && m.ToUser == username && !m.IsRead)
                .ToListAsync();
            foreach (var msg in unread)
            {
                msg.IsRead = true;
            }
            await _db.SaveChangesAsync();
            
            var result = messages.OrderBy(m => m.SentAt).Select(m => new {
                id = m.Id,
                from = m.FromUser,
                text = m.Message,
                time = m.SentAt.ToString("HH:mm"),
                isMine = m.FromUser == username
            });
            
            return Json(result);
        }
        
        // Отправить сообщение
        [HttpPost]
        public async Task<IActionResult> Send(string toUser, string message)
        {
            var fromUser = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(fromUser)) return Json(new { success = false, error = "Not authenticated" });
            
            if (string.IsNullOrWhiteSpace(message))
                return Json(new { success = false, error = "Message cannot be empty" });
            
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
            
            return Json(new { 
                success = true, 
                id = msg.Id,
                time = msg.SentAt.ToString("HH:mm")
            });
        }
        
        // Поиск пользователей
        [HttpGet]
        public async Task<IActionResult> SearchUsers(string query)
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return Json(new List<object>());
            
            if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
                return Json(new List<object>());
            
            var users = await _db.Users
                .Where(u => u.Username != username && 
                            (u.Username.Contains(query) || u.Tag.Contains(query)))
                .Take(10)
                .Select(u => new { u.Username, u.Tag, u.IsAdmin })
                .ToListAsync();
            
            return Json(users);
        }
    }
}