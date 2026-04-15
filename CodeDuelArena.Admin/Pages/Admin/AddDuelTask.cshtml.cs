using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeDuelArena.Admin.Data;
using CodeDuelArena.Admin.Models;

namespace CodeDuelArena.Admin.Pages.Admin;

public class AddDuelTaskModel : PageModel
{
    private readonly AppDbContext _db;
    public AddDuelTaskModel(AppDbContext db) => _db = db;
    [BindProperty] public DuelTask Task { get; set; } = new();

    public IActionResult OnGet()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        return Page();
    }

    public IActionResult OnPost()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        Task.CreatedAt = DateTime.UtcNow;
        _db.DuelTasks.Add(Task);
        _db.SaveChanges();
        TempData["Message"] = "Задание добавлено!";
        return RedirectToPage("/Admin/DuelTasks");
    }
}