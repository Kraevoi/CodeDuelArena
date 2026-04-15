using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Admin.Data;
using CodeDuelArena.Admin.Models;

namespace CodeDuelArena.Admin.Pages.Admin;

public class DashboardModel : PageModel
{
    private readonly AppDbContext _db;
    public DashboardModel(AppDbContext db) => _db = db;

    public int TotalUsers { get; set; }
    public int TotalScore { get; set; }
    public int ActiveToday { get; set; }
    public int TotalWins { get; set; }
    public int UnreadComplaints { get; set; }

    public IActionResult OnGet()
    {
        if (Request.Cookies["admin_auth"] != "true")
            return RedirectToPage("/Admin/Login");

        var users = _db.Users.ToList();
        TotalUsers = users.Count;
        TotalScore = users.Sum(u => u.Score);
        ActiveToday = users.Count(u => u.LastLogin.Date == DateTime.UtcNow.Date);
        TotalWins = users.Sum(u => u.Wins);
        UnreadComplaints = _db.Complaints.Count(c => !c.IsRead);
        return Page();
    }
}