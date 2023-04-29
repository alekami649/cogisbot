using CoGISBot.Telegram.Processing;
using Microsoft.AspNetCore.Mvc;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CoGISBot.Telegram.Controllers;

[ApiController]
public class WebhookController : ControllerBase
{
    readonly TelegramBotClient botClient;
    static object ErrorsFile = new();

    public WebhookController(TelegramBotClient _botClient)
    {
        botClient = _botClient;
    }

    [Route("webhook/telegram")]
    [HttpPost]
    public async Task<IActionResult> ProcesUpdate(Update update)
    {
        try
        {
            await TelegramProcessing.ProcessUpdate(botClient, update);
            return Ok();
        }
        catch (Exception ex)
        {
            lock (ErrorsFile)
            {
                System.IO.File.AppendAllText("errors_webhook.txt", ex.ToString() + Environment.NewLine);
            }
            return Forbid();
        }
    }
}