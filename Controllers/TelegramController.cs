using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using CodeDuelArena.Services;

namespace CodeDuelArena.Controllers
{
    [ApiController]
    [Route("api/telegram")]
    public class TelegramController : ControllerBase
    {
        private readonly TelegramBotService _botService;

        public TelegramController(TelegramBotService botService)
        {
            _botService = botService;
        }

        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] JObject update)
        {
            // Определяем тип обновления
            if (update["callback_query"] != null)
            {
                var result = await _botService.HandleCallback(update);
                return Ok(new { status = result });
            }
            else if (update["message"] != null)
            {
                var result = await _botService.HandleUpdate(update);
                return Ok(new { status = result });
            }

            return Ok(new { status = "ignored" });
        }

        [HttpGet("setup")]
        public async Task<IActionResult> SetupWebhook()
        {
            await _botService.SetWebhook();
            return Ok(new { status = "webhook setup initiated" });
        }
    }
}