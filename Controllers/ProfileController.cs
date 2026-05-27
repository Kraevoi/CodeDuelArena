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
            if (settings == null)
            {
                settings = new UserSettings { Username = username };
            }
            
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
            
            if (avatar != null && avatar.Length > 0 && avatar.Length < 2 * 1024 * 1024)
            {
                var contentType = avatar.ContentType.ToLower();
                if (contentType != "image/png" && contentType != "image/jpeg" && contentType != "image/jpg" && contentType != "image/gif")
                {
                    TempData["Error"] = "Only PNG, JPG, GIF allowed.";
                    return RedirectToAction("Index");
                }
                
                using var ms = new MemoryStream();
                await avatar.CopyToAsync(ms);
                var avatarData = ms.ToArray();
                
                // Конвертируем в PNG через SkiaSharp
                using var inputStream = new SKMemoryStream(avatarData);
                using var codec = SKCodec.Create(inputStream);
                using var bitmap = SKBitmap.Decode(codec);
                
                // Ресайз до 300x300 максимум
                var maxSize = 300;
                if (bitmap.Width > maxSize || bitmap.Height > maxSize)
                {
                    var scale = Math.Min((float)maxSize / bitmap.Width, (float)maxSize / bitmap.Height);
                    var newWidth = (int)(bitmap.Width * scale);
                    var newHeight = (int)(bitmap.Height * scale);
                    using var resized = bitmap.Resize(new SKImageInfo(newWidth, newHeight), SKFilterQuality.High);
                    using var image = SKImage.FromBitmap(resized);
                    using var pngData = image.Encode(SKEncodedImageFormat.Png, 90);
                    avatarData = pngData.ToArray();
                }
                else
                {
                    using var image = SKImage.FromBitmap(bitmap);
                    using var pngData = image.Encode(SKEncodedImageFormat.Png, 90);
                    avatarData = pngData.ToArray();
                }
                
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
            }
            else
            {
                TempData["Error"] = "File too large. Maximum 2MB.";
            }
            
            return RedirectToAction("Index");
        }
        
        [HttpGet]
        [Route("/Profile/Avatar")]
        public async Task<IActionResult> Avatar(string username)
        {
            // Пробуем из БД
            if (!string.IsNullOrEmpty(username))
            {
                var settings = await _db.UserSettings.FirstOrDefaultAsync(s => s.Username == username);
                if (settings?.AvatarData != null && settings.AvatarData.Length > 0)
                {
                    return File(settings.AvatarData, "image/png");
                }
            }
            
            // Генерируем дефолтный
            var letter = string.IsNullOrEmpty(username) ? "?" : username[0].ToString().ToUpper();
            var png = GenerateDefaultAvatar(letter);
            return File(png, "image/png");
        }
        
        private byte[] GenerateDefaultAvatar(string letter)
        {
            int size = 300;
            using var surface = SKSurface.Create(new SKImageInfo(size, size));
            var canvas = surface.Canvas;
            
            // Красный фон с круглыми углами
            using var bgPaint = new SKPaint { Color = new SKColor(220, 53, 69), IsAntialias = true };
            canvas.DrawRoundRect(new SKRoundRect(new SKRect(0, 0, size, size), 40), bgPaint);
            
            // Белая буква
            using var textPaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                TextSize = size * 0.55f,
                TextAlign = SKTextAlign.Center,
                Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
            };
            
            canvas.DrawText(letter, size / 2f, size * 0.7f, textPaint);
            
            using var image = surface.Snapshot();
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