using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using Newtonsoft.Json;
namespace CodeDuelArena.Controllers
{
    [Route(""api/[controller]"")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminController(AppDbContext db)
        {
            _db = db;
        }
        [HttpPost(""DeleteUser"")]
        public async Task<IActionResult> DeleteUser([FromForm] int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user != null)
            {
                _db.Users.Remove(user);
                await _db.SaveChangesAsync();
                return Ok(new { success = true });
            }
            return Ok(new { success = false });
        }
        [HttpPost(""DeleteQuest"")]
        public IActionResult DeleteQuest([FromForm] int id)
        {
            var quests = DataStorage.GetQuests();
            var quest = quests.FirstOrDefault(q => q.Id == id);
            if (quest != null)
            {
                quests.Remove(quest);
                var json = JsonConvert.SerializeObject(quests, Formatting.Indented);
                System.IO.File.WriteAllText(""quests.json"", json);
                return Ok(new { success = true });
            }
            return Ok(new { success = false });
        }
        [HttpPost(""MarkRead"")]
        public async Task<IActionResult> MarkRead([FromForm] int id)
        {
            var complaint = await _db.Complaints.FindAsync(id);
            if (complaint != null)
            {
                complaint.IsRead = true;
                await _db.SaveChangesAsync();
                return Ok(new { success = true });
            }
            return Ok(new { success = false });
        }
        [HttpPost(""DeleteComplaint"")]
        public async Task<IActionResult> DeleteComplaint([FromForm] int id)
        {
            var complaint = await _db.Complaints.FindAsync(id);
            if (complaint != null)
            {
                _db.Complaints.Remove(complaint);
                await _db.SaveChangesAsync();
                return Ok(new { success = true });
            }
            return Ok(new { success = false });
        }
    }
}
