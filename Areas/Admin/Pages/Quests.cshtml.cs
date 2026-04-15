using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using CodeDuelArena.Data;
using CodeDuelArena.Models;
using Newtonsoft.Json;

namespace CodeDuelArena.Areas.Admin.Pages;

public class QuestsModel : PageModel
{
    public List<QuestModel> Quests { get; set; } = new();

    public IActionResult OnGet()
    {
        if (Request.Cookies["admin_auth"] != "true") return RedirectToPage("/Admin/Login");
        Quests = DataStorage.GetQuests();
        return Page();
    }

    public IActionResult OnPostDelete(int id)
    {
        if (Request.Cookies["admin_auth"] != "true") return new JsonResult(new { success = false });
        var quests = DataStorage.GetQuests();
        var q = quests.FirstOrDefault(x => x.Id == id);
        if (q != null)
        {
            quests.Remove(q);
            System.IO.File.WriteAllText("quests.json", JsonConvert.SerializeObject(quests, Formatting.Indented));
            return new JsonResult(new { success = true });
        }
        return new JsonResult(new { success = false });
    }
}