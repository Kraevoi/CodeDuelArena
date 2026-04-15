using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeDuelArena.Admin.Data;
using CodeDuelArena.Admin.Models;

namespace CodeDuelArena.Admin.Pages.Admin;

public class ComplaintsModel : PageModel
{
    private readonly AppDbContext _db;
    public ComplaintsModel(AppDbContext db) => _db = db;
    public List<Complaint> Complaints { get; set; } = new();

    public IActionResult OnGet()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        Complaints = _db.Complaints.OrderByDescending(c => c.CreatedAt).ToList();
        return Page();
    }

    public IActionResult OnPostMarkRead(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var c = _db.Complaints.Find(id);
        if (c != null) { c.IsRead = true; _db.SaveChanges(); return new JsonResult(new { success = true }); }
        return new JsonResult(new { success = false });
    }

    public IActionResult OnPostDelete(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var c = _db.Complaints.Find(id);
        if (c != null) { _db.Complaints.Remove(c); _db.SaveChanges(); return new JsonResult(new { success = true }); }
        return new JsonResult(new { success = false });
    }
}