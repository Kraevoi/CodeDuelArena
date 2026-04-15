using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeDuelArena.Data;
using CodeDuelArena.Models;
using Newtonsoft.Json;

namespace CodeDuelArena.Areas.Admin.Pages;

public class AddQuestModel : PageModel
{
    [BindProperty] public QuestModel Quest { get; set; } = new();

    public IActionResult OnGet()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        return Page();
    }

    public IActionResult OnPost()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        
        var quests = DataStorage.GetQuests();
        Quest.Id = quests.Any() ? quests.Max(q => q.Id) + 1 : 1;
        quests.Add(Quest);
        
        System.IO.File.WriteAllText("quests.json", JsonConvert.SerializeObject(quests, Formatting.Indented));
        
        TempData["Message"] = $"Квест '{Quest.Title}' добавлен!";
        return RedirectToPage("/Admin/Quests");
    }
}