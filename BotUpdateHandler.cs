using CoGISBot.Telegram.Processing;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;

namespace CoGISBot.Telegram;

public class BotUpdateHandler : IUpdateHandler
{
    public Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        await TelegramProcessing.ProcessUpdate((TelegramBotClient)botClient, update);
    }
}
