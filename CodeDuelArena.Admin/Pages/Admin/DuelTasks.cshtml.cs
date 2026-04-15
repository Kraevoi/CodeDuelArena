using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeDuelArena.Admin.Data;
using CodeDuelArena.Admin.Models;

namespace CodeDuelArena.Admin.Pages.Admin;

public class DuelTasksModel : PageModel
{
    private readonly AppDbContext _db;
    public DuelTasksModel(AppDbContext db) => _db = db;
    public List<DuelTask> Tasks { get; set; } = new();

    public IActionResult OnGet()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        Tasks = _db.DuelTasks.OrderByDescending(t => t.CreatedAt).ToList();
        return Page();
    }

    public IActionResult OnPostToggle(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var t = _db.DuelTasks.Find(id);
        if (t != null) { t.IsActive = !t.IsActive; _db.SaveChanges(); return new JsonResult(new { success = true }); }
        return new JsonResult(new { success = false });
    }

    public IActionResult OnPostDelete(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var t = _db.DuelTasks.Find(id);
        if (t != null) { _db.DuelTasks.Remove(t); _db.SaveChanges(); return new JsonResult(new { success = true }); }
        return new JsonResult(new { success = false });
    }
}