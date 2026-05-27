using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CodeDuelArena.Data;
using CodeDuelArena.Models;
using SkiaSharp;

namespace CodeDuelArena.Controllers
{
    public class ProfileController : Controller
    {
        private readonly AppDbContext _db;
        
        public ProfileController(AppDbContext db)
        {
            _db = db;
        }
        
        [HttpGet]
        public async Task<IActionResult> Index(string username)
        {
            var currentUser = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) username = currentUser ?? "";
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Index", "Home");
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound();
            
            var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
            if (settings == null) settings = new UserSettings { Username = username };
            
            var league = await _db.UserLeagues.FirstOrDefaultAsync(l => l.Username == username);
            var achievements = await _db.UserAchievements
                .Where(a => a.Username == username)
                .Include(a => a.Achievement)
                .ToListAsync();
            
            ViewBag.Settings = settings;
            ViewBag.League = league;
            ViewBag.Achievements = achievements;
            ViewBag.IsOwnProfile = currentUser == username;
            
            return View(user);
        }
        
        [HttpPost]
        public async Task<IActionResult> UploadAvatar(IFormFile avatar)
        {
            var username = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(username)) return RedirectToAction("Index", "Home");
            
            if (avatar == null || avatar.Length == 0)
            {
                TempData["Error"] = "No file selected.";
                return RedirectToAction("Index");
            }
            
            if (avatar.Length > 2 * 1024 * 1024)
            {
                TempData["Error"] = "File too large. Maximum 2MB.";
                return RedirectToAction("Index");
            }
            
            var contentType = avatar.ContentType.ToLower();
            if (contentType != "image/png" && contentType != "image/jpeg" && contentType != "image/jpg" && contentType != "image/gif")
            {
                TempData["Error"] = "Only PNG, JPG, GIF allowed.";
                return RedirectToAction("Index");
            }
            
            using var ms = new MemoryStream();
            await avatar.CopyToAsync(ms);
            var originalData = ms.ToArray();
            
            // Конвертируем в PNG 300x300
            using var inputStream = new SKMemoryStream(originalData);
            using var codec = SKCodec.Create(inputStream);
            if (codec == null)
            {
                TempData["Error"] = "Invalid image file.";
                return RedirectToAction("Index");
            }
            
            using var originalBitmap = SKBitmap.Decode(codec);
            if (originalBitmap == null)
            {
                TempData["Error"] = "Cannot decode image.";
                return RedirectToAction("Index");
            }
            
            // Ресайз в квадрат 300x300
            int size = 300;
            using var squareBitmap = new SKBitmap(size, size);
            using var canvas = new SKCanvas(squareBitmap);
            
            // Считаем размеры чтобы вписать с обрезкой (cover)
            float scale = Math.Max((float)size / originalBitmap.Width, (float)size / originalBitmap.Height);
            float scaledWidth = originalBitmap.Width * scale;
            float scaledHeight = originalBitmap.Height * scale;
            float left = (size - scaledWidth) / 2f;
            float top = (size - scaledHeight) / 2f;
            
            canvas.DrawBitmap(originalBitmap, new SKRect(left, top, left + scaledWidth, top + scaledHeight));
            
            using var image = SKImage.FromBitmap(squareBitmap);
            using var pngData = image.Encode(SKEncodedImageFormat.Png, 95);
            var avatarData = pngData.ToArray();
            
            var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
            if (settings == null)
            {
                settings = new UserSettings { Username = username };
                _db.UserSettings.Add(settings);
            }
            
            settings.AvatarData = avatarData;
            settings.AvatarContentType = "image/png";
            settings.AvatarUrl = "";
            
            await _db.SaveChangesAsync();
            TempData["Message"] = "Avatar updated!";
            
            return RedirectToAction("Index");
        }
        
        [HttpGet]
        [Route("/Profile/Avatar")]
        public async Task<IActionResult> Avatar(string username)
        {
            byte[] png;
            
            if (!string.IsNullOrEmpty(username))
            {
                var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
                if (settings?.AvatarData != null && settings.AvatarData.Length > 0)
                {
                    return File(settings.AvatarData, "image/png");
                }
            }
            
            png = GenerateDefaultAvatar(string.IsNullOrEmpty(username) ? "?" : username[0].ToString().ToUpper());
            return File(png, "image/png");
        }
        
        private byte[] GenerateDefaultAvatar(string letter)
        {
            int size = 300;
            using var bitmap = new SKBitmap(size, size);
            using var canvas = new SKCanvas(bitmap);
            
            // Красный фон
            var bgColor = new SKColor(220, 53, 69);
            canvas.Clear(bgColor);
            
            // Белая буква по центру
            using var paint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                TextSize = 160,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };
            
            // Центрируем текст
            float textY = size / 2f + paint.TextSize / 3f;
            canvas.DrawText(letter, size / 2f, textY, paint);
            
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }
        
        [HttpPost]
        public async Task<IActionResult> ChangeUsername(string newUsername)
        {
            var currentUsername = Request.Cookies["auth_user"];
            if (string.IsNullOrEmpty(currentUsername))
                return Json(new { success = false, error = "Not authenticated" });
            
            if (string.IsNullOrWhiteSpace(newUsername) || newUsername.Length < 3)
                return Json(new { success = false, error = "Username must be at least 3 characters" });
            
            if (newUsername == currentUsername)
                return Json(new { success = false, error = "This is already your username" });
            
            var exists = await _db.Users.AnyAsync(u => u.Username == newUsername);
            if (exists)
                return Json(new { success = false, error = "Username already taken" });
            
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
            if (user == null)
                return Json(new { success = false, error = "User not found" });
            
            user.Username = newUsername;
            await _db.SaveChangesAsync();
            
            Response.Cookies.Append("auth_user", newUsername, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = DateTime.Now.AddDays(30)
            });
            
            return Json(new { success = true, newUsername = newUsername });
        }
    }
}