using Microsoft.AspNetCore.Mvc;
using Relict_TelegramBot_Stride.BotControllers;
using Relict_TelegramBot_Stride.DTO;

namespace Relict_TelegramBot_Stride.Controllers
{
    [ApiController]
    [Route("api/bot")]
    public class BotCallbackController : ControllerBase
    {
        private readonly BotService _botService;

        public BotCallbackController(BotService botService)
        {
            _botService = botService;
        }

        [HttpPost("send")]
        public async Task<IActionResult> Send([FromBody] SendMessageDto dto)
        {
            await Task.Run(() => _botService.SendMessageAsync(dto.ChatId, dto.Text));
            return Ok(new { message = "Sent" });
        }
    }
}
