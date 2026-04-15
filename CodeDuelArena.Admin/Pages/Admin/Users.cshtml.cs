using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeDuelArena.Admin.Data;
using CodeDuelArena.Admin.Models;

namespace CodeDuelArena.Admin.Pages.Admin;

public class UsersModel : PageModel
{
    private readonly AppDbContext _db;
    public UsersModel(AppDbContext db) => _db = db;
    public List<UserDb> Users { get; set; } = new();

    public IActionResult OnGet()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        Users = _db.Users.OrderByDescending(u => u.Score).ToList();
        return Page();
    }

    public IActionResult OnPostDelete(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var u = _db.Users.Find(id);
        if (u != null) { _db.Users.Remove(u); _db.SaveChanges(); return new JsonResult(new { success = true }); }
        return new JsonResult(new { success = false });
    }
}