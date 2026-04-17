using CodeDuelArena.Data;
using CodeDuelArena.Hubs;
using CodeDuelArena.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connStr));

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<DailyQuestService>();
builder.Services.AddScoped<AchievementService>();
builder.Services.AddScoped<LeagueService>();
builder.Services.AddHostedService<TelegramBotService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    
    var dailyService = scope.ServiceProvider.GetRequiredService<DailyQuestService>();
    await dailyService.InitializeDailyQuests();
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapHub<DuelHub>("/duelHub");

app.Run();