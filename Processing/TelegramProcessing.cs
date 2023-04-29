using Microsoft.AspNetCore.Builder.Extensions;
using Newtonsoft.Json;
using System.Data.Common;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

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
    public static string GeocoderUrl { get; set; } = System.IO.File.ReadAllText("geocoderUrl.txt");
    public static string CadastreUrl { get; set; } = System.IO.File.ReadAllText("cadastreUrl.txt");
    static object ErrorsFile { get; set; } = new();
    public static CatalogNodesReponse CatalogNodes { get; set; } = FetchCatalogNodes();

    public static async Task ProcessUpdate(TelegramBotClient botClient, Update update)
    {
        try
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
        catch (Exception ex)
        {
            lock (ErrorsFile)
            {
                System.IO.File.AppendAllText("errors_update.txt", ex.ToString() + Environment.NewLine);
            }
        }
    }

    #region Inline Queries
    public static async Task ProcessInlineQuery(TelegramBotClient botClient, InlineQuery inlineQuery)
    {
        try
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
        catch (Exception ex)
        {
            lock (ErrorsFile)
            {
                System.IO.File.AppendAllText("errors_inlinequeries.txt", ex.ToString() + Environment.NewLine);
            }
        }
    }
    #endregion

    #region Messages
    public static async Task ProcessMessage(TelegramBotClient botClient, Message message)
    {
        try
        {
            if (message.Type == MessageType.Text && message.Text != null)
            {
                await ProcessTextMessage(botClient, message);
            }
        }
        catch (Exception ex)
        {
            lock (ErrorsFile)
            {
                System.IO.File.AppendAllText("errors_messages.txt", ex.ToString() + Environment.NewLine);
            }
        }
    }

    public static async Task ProcessTextMessage(TelegramBotClient botClient, Message message)
    {
        try
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
                message.Text = message.Text.Trim();
                if (message.Text == "/get_catalog" || message.Text == $"/get_catalog@{(await botClient.GetMeAsync()).Username}")
                {
                    CatalogNodes = FetchCatalogNodes();
                }
                else if (message.Text == "/start" || message.Text == $"/start@{(await botClient.GetMeAsync()).Username}")
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Здраствуйте, {message.From.FirstName}.\nВы можете просто написать Ваш запрос в данный диалог, и бот автоматически найдёт результаты по запросу - карты, адреса, кадастровые номера.\nТакже в любом диалоге Вы можете воспользоваться ```@ {(await botClient.GetMeAsync()).Username ?? "cogisdemo_bot" } (Ваш запрос)```, Вам выдадут список карт по запросу, и возможность отправить любую карту в текущий чат.", ParseMode.Markdown);
                }
                else if (message.Text.StartsWith('/'))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, $"Вы попытались использовать команду \"{message.Text}\", но она не обрабатывается ботом.");
                }
                else if (message.Text.StartsWith('@'))
                {
                    return;
                }
                else
                {
                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    #region Maps
                    var mapsResults = CatalogNodes.Search(message.Text).DistinctBy(x => x.Caption).Take(5);
                    var keyboard = null as InlineKeyboardMarkup;
                    if (!mapsResults.Any())
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "По Вашему запросу карт не найдено.");
                    }
                    else
                    {
                        var builder = new StringBuilder();
                        builder.AppendLine($"Карты <a href=\"{BrandURL}\">{BrandName}</a> по запросу \"{message.Text}\":\n");

                        if (mapsResults.Count() == 1)
                        {
                            builder.AppendLine($"- <a href=\"{mapsResults.First().GetUrl()}\">{mapsResults.First().Caption}</a>");
                            if (mapsResults.First().Info.DescriptionLink != "" && mapsResults.First().Info.DescriptionCaption != "")
                            {
                                builder.AppendLine("  - " + mapsResults.First().Info.GetDescriptionText());
                            }
                            var info = new WebAppInfo()
                            {
                                Url = mapsResults.First().GetUrl()
                            };
                            keyboard = new InlineKeyboardMarkup(new InlineKeyboardButton[] { InlineKeyboardButton.WithUrl("Открыть в браузере", mapsResults.First().GetUrl()),
                                                                                         InlineKeyboardButton.WithWebApp("Открыть в Telegram", info) });
                        }
                        else
                        {
                            foreach (var result in mapsResults)
                            {
                                builder.AppendLine($"- <a href=\"{result.GetUrl()}\">{result.Caption}</a>");
                                if (result.Info.DescriptionLink != "" && result.Info.DescriptionCaption != "")
                                {
                                    builder.AppendLine("  - " + result.Info.GetDescriptionText());
                                }
                                builder.AppendLine();
                            }
                        }
                        await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), ParseMode.Html, replyMarkup: keyboard, replyToMessageId: message.MessageId);
                    }
                    #endregion
                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    #region Addresses
                    var addressResults = (await GeocoderProcessing.FindAddressCandidates(message.Text, GeocoderUrl)).Candidates.Where(x => x.Address.Length > 2).Take(5).ToList();
                    if (!addressResults.Any())
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "По Вашему запросу адресов не найдено.");
                    }
                    else
                    {
                        var builder = new StringBuilder();
                        builder.AppendLine($"Адреса в <a href=\"{BrandURL}\">{BrandName}</a> по запросу \"{message.Text}\":\n");
                        foreach (var result in addressResults)
                        {
                            builder.AppendLine($"- {result.Address}"); //TODO: url
                                                                       //if (result.Info.DescriptionLink != "" && result.Info.DescriptionCaption != "")
                                                                       //{
                                                                       //    builder.AppendLine("  - " + result.Info.GetDescriptionText());
                                                                       //}
                                                                       //builder.AppendLine();
                        }
                        await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), ParseMode.Html, replyToMessageId: message.MessageId);
                    }
                    #endregion
                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    #region Cadastre
                    var cadastreResults = (await CadastreProcessing.Find(message.Text, CadastreUrl)).Results;
                    if (!cadastreResults.Any())
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, "По Вашему запросу кадастровых номеров не найдено.");
                    }
                    else
                    {
                        var builder = new StringBuilder();
                        builder.AppendLine($"Кадастровые номера в <a href=\"{BrandURL}\">{BrandName}</a> по запросу \"{message.Text}\":\n");
                        foreach (var result in cadastreResults)
                        {
                            builder.AppendLine($"- {result.Attributes.Address} ({result.Attributes.Number})"); //TODO: url
                                                                                                               //if (result.Info.DescriptionLink != "" && result.Info.DescriptionCaption != "")
                                                                                                               //{
                                                                                                               //    builder.AppendLine("  - " + result.Info.GetDescriptionText());
                                                                                                               //}
                                                                                                               //builder.AppendLine();
                        }
                        await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), ParseMode.Html, replyToMessageId: message.MessageId);
                    }
                    #endregion
                }
            }
        }
        catch (Exception ex)
        {
            lock (ErrorsFile)
            {
                System.IO.File.AppendAllText("errors_textmessages.txt", ex.ToString() + Environment.NewLine);
            }
        }
        finally
        {

        }
    }
    #endregion
}