using CoGISBot.Telegram.Processing;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CoGISBot.Telegram.Controllers;

[ApiController]
public class WebhookController : ControllerBase
{
    readonly TelegramBotClient botClient;

    public WebhookController(TelegramBotClient _botClient)
    {
        botClient = _botClient;
    }

    [Route("webhook/telegram")]
    [HttpPost]
    public async Task<IActionResult> ProcesUpdate(Update update)
    {
        await TelegramProcessing.ProcessUpdate(botClient, update);
        return Ok();
    }
}