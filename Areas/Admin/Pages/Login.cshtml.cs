using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Security.Cryptography;
using System.Text;

namespace CodeDuelArena.Areas.Admin.Pages;

public class LoginModel : PageModel
{
    private static readonly string AdminPasswordHash = HashPassword("admin123");

    [BindProperty] public string Password { get; set; } = "";
    [BindProperty] public bool RememberMe { get; set; }
    public string? ErrorMessage { get; set; }

    public IActionResult OnGet()
    {
        if (Request.Cookies["admin_auth"] == "true") return RedirectToPage("/Dashboard");
        return Page();
    }

    public IActionResult OnPost()
    {
        if (VerifyPassword(Password, AdminPasswordHash))
        {
            Response.Cookies.Append("admin_auth", "true", new CookieOptions
            {
                HttpOnly = true,
                Expires = RememberMe ? DateTime.Now.AddDays(7) : DateTime.Now.AddHours(1)
            });
            return RedirectToPage("/Dashboard");
        }
        ErrorMessage = "Неверный пароль";
        return Page();
    }

    public IActionResult OnGetLogout()
    {
        Response.Cookies.Delete("admin_auth");
        return RedirectToPage("/Login");
    }

    private static string HashPassword(string p) => Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(p)));
    private static bool VerifyPassword(string p, string h) => HashPassword(p) == h;
}