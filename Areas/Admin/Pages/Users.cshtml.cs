using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeDuelArena.Data;
using CodeDuelArena.Models;

namespace CodeDuelArena.Areas.Admin.Pages;

public class UsersModel : PageModel
{
    private readonly AppDbContext _db;
    public UsersModel(AppDbContext db) => _db = db;
    
    public List<UserDb> Users { get; set; } = new();
    public string CurrentSort { get; set; } = "score_desc";

    public IActionResult OnGet(string sort = "score_desc")
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        
        CurrentSort = sort;
        var usersQuery = _db.Users.AsQueryable();
        
        Users = sort switch
        {
            "name_asc" => usersQuery.OrderBy(u => u.Username).ToList(),
            "name_desc" => usersQuery.OrderByDescending(u => u.Username).ToList(),
            "score_desc" => usersQuery.OrderByDescending(u => u.Score).ToList(),
            "score_asc" => usersQuery.OrderBy(u => u.Score).ToList(),
            "date_desc" => usersQuery.OrderByDescending(u => u.RegisteredAt).ToList(),
            "date_asc" => usersQuery.OrderBy(u => u.RegisteredAt).ToList(),
            _ => usersQuery.OrderByDescending(u => u.Score).ToList()
        };
        
        return Page();
    }

    public IActionResult OnPostDelete(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var u = _db.Users.Find(id);
        if (u != null)
        {
            _db.Users.Remove(u);
            _db.SaveChanges();
            return new JsonResult(new { success = true });
        }
        return new JsonResult(new { success = false });
    }
}