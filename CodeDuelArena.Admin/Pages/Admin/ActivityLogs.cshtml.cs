using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeDuelArena.Admin.Data;
using CodeDuelArena.Admin.Models;

namespace CodeDuelArena.Admin.Pages.Admin;

public class ActivityLogsModel : PageModel
{
    private readonly AppDbContext _db;
    public ActivityLogsModel(AppDbContext db) => _db = db;
    public List<ActivityLog> Logs { get; set; } = new();

    public IActionResult OnGet()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        Logs = _db.ActivityLogs.OrderByDescending(l => l.Timestamp).Take(500).ToList();
        return Page();
    }
}