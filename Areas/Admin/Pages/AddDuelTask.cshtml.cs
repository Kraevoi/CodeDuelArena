using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeDuelArena.Data;
using CodeDuelArena.Models;

namespace CodeDuelArena.Areas.Admin.Pages;

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

    public async Task<IActionResult> OnPostAsync()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        
        Task.CreatedAt = DateTime.UtcNow;
        _db.DuelTasks.Add(Task);
        await _db.SaveChangesAsync();
        
        TempData["Message"] = $"Задание '{Task.Title}' добавлено!";
        return RedirectToPage("/Admin/DuelTasks");
    }
}