using Newtonsoft.Json;
using System.Data.Common;
using System.Runtime.InteropServices;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;

namespace CoGISBot.Telegram.Processing;

public class TelegramProcessing
{
    public static CatalogNodesReponse FetchCatalogNodes()
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("https://cogisdemo.dataeast.com/portal/Catalog/GetCatalogNodes"));
        using var response = client.Send(request);
        return JsonConvert.DeserializeObject<CatalogNodesReponse>(response.Content.ReadAsStringAsync().Result) ?? new();
    }

    public static string BrandName { get; set; } = System.IO.File.ReadAllText("brandname.txt");
    public static string BrandURL { get; set; } = System.IO.File.ReadAllText("brandurl.txt");
    public static CatalogNodesReponse CatalogNodes { get; set; } = FetchCatalogNodes();

    public static async Task ProcessUpdate(TelegramBotClient botClient, Update update)
    {
        if (update.Type == UpdateType.Message && update.Message != null)
        {
            await ProcessMessage(botClient, update.Message);
        }
        else if (update.Type == UpdateType.InlineQuery && update.InlineQuery != null)
        {
            await ProcessInlineQuery(botClient, update.InlineQuery);
        }
    }

    #region Inline Queries
    public static async Task ProcessInlineQuery(TelegramBotClient botClient, InlineQuery inlineQuery)
    {
        var result = new List<InlineQueryResult>();
        var query = inlineQuery.Query.Trim();
        if (query == null || query == "" || string.IsNullOrWhiteSpace(query))
        {
            var globalMaps = CatalogNodes.GetMaps(50);
            var photos = globalMaps.Select(x => x.GetArticle());
            result.AddRange(photos);
        }
        else
        {
            var maps = CatalogNodes.Search(query).DistinctBy(x => x.Caption);
            if (!maps.Any())
            {
                return;
            }
            else
            {
                var photos = maps.Select(x => x.GetArticle());
                result.AddRange(photos);
            }
        }
        await botClient.AnswerInlineQueryAsync(inlineQuery.Id, result.Take(50), 1, false);
    }
    #endregion

    #region Messages
    public static async Task ProcessMessage(TelegramBotClient botClient, Message message)
    {
        if (message.Type == MessageType.Text && message.Text != null)
        {
            await ProcessTextMessage(botClient, message);
        }
    }

    public static async Task ProcessTextMessage(TelegramBotClient botClient, Message message)
    {
        if (message.Text == null || message.Type != MessageType.Text)
        {
            return;
        }
        if (message.From == null || message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Supergroup || message.Chat.Type == ChatType.Channel)
        {
            await botClient.LeaveChatAsync(message.Chat);
            return;
        }
        if (message.Chat.Type == ChatType.Private)
        {
            if (message.Text == "/get_catalog" || message.Text == $"/get_catalog@{(await botClient.GetMeAsync()).Username}")
            {
                CatalogNodes = FetchCatalogNodes();
            }
            else if (message.Text.StartsWith('/'))
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, $"Вы попытались использовать команду \"{message.Text}\", но она не обрабатывается ботом.");
            }
            else
            {
                var results = CatalogNodes.Search(message.Text);
                if (!results.Any())
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, "По Вашему запросу не найдено результатов.");
                }
                else
                {
                    var builder = new StringBuilder();
                    builder.AppendLine($"Карты <a href=\"{BrandURL}\">{BrandName}</a> по запросу \"{message.Text}\":\n");
                    foreach (var result in results.DistinctBy(x => x.Caption))
                    {
                        builder.AppendLine($"<a href=\"{result.GetUrl()}\">{result.Caption}</a>");
                    }
                    await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), ParseMode.Html);
                }
            }
        }
    }
    #endregion
}