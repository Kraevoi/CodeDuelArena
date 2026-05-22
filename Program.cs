using CodeDuelArena.Data;
using CodeDuelArena.Hubs;
using CodeDuelArena.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();


builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "Cookies";
})
.AddCookie("Cookies", options =>
{
    options.Cookie.Name = "auth_user";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.MaxAge = TimeSpan.FromDays(30);
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

builder.Services.AddScoped<DailyQuestService>();
builder.Services.AddScoped<AchievementService>();
builder.Services.AddScoped<LeagueService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();

    try
    {
        await db.Database.ExecuteSqlRawAsync(
            @"ALTER TABLE ""UserSettings"" 
              ADD COLUMN IF NOT EXISTS ""TelegramChatId"" text NOT NULL DEFAULT '',
              ADD COLUMN IF NOT EXISTS ""NotifyTournaments"" boolean NOT NULL DEFAULT false,
              ADD COLUMN IF NOT EXISTS ""NotifyTechUpdates"" boolean NOT NULL DEFAULT false;");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Migration: {ex.Message}");
    }

    var dailyService = scope.ServiceProvider.GetRequiredService<DailyQuestService>();
    await dailyService.InitializeDailyQuests();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<DuelHub>("/duelHub");

app.Run();