using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using CodeDuelArena.Models;

namespace CodeDuelArena.Areas.Admin.Pages;

public class DuelTasksModel : PageModel
{
    private readonly AppDbContext _db;
    public DuelTasksModel(AppDbContext db) => _db = db;
    public List<DuelTask> Tasks { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        Tasks = await _db.DuelTasks.OrderByDescending(t => t.CreatedAt).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var t = await _db.DuelTasks.FindAsync(id);
        if (t != null)
        {
            t.IsActive = !t.IsActive;
            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true, isActive = t.IsActive });
        }
        return new JsonResult(new { success = false });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var t = await _db.DuelTasks.FindAsync(id);
        if (t != null)
        {
            _db.DuelTasks.Remove(t);
            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }
        return new JsonResult(new { success = false });
    }
}