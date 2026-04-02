using CodeDuelArena.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapHub<DuelHub>("/duelHub");

app.Run();