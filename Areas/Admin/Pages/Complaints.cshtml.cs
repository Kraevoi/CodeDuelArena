using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using CodeDuelArena.Models;

namespace CodeDuelArena.Areas.Admin.Pages;

public class ComplaintsModel : PageModel
{
    private readonly AppDbContext _db;
    public ComplaintsModel(AppDbContext db) => _db = db;
    public List<Complaint> Complaints { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        Complaints = await _db.Complaints.OrderByDescending(c => c.CreatedAt).ToListAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostMarkReadAsync(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var c = await _db.Complaints.FindAsync(id);
        if (c != null)
        {
            c.IsRead = true;
            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }
        return new JsonResult(new { success = false });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var c = await _db.Complaints.FindAsync(id);
        if (c != null)
        {
            _db.Complaints.Remove(c);
            await _db.SaveChangesAsync();
            return new JsonResult(new { success = true });
        }
        return new JsonResult(new { success = false });
    }
}