using CoGISBot.Telegram.Helpers;
using Newtonsoft.Json;
using System.Globalization;
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

    public static string DefaultBotUsername { get; set; } = "cogisdemo_bot";
    public static Dictionary<long, UserState> States { get; set; } = new();
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
            Resources.Culture = CultureInfo.GetCultureInfo(inlineQuery.From.LanguageCode ?? "ru");
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
            await botClient.AnswerInlineQueryAsync(inlineQuery.Id, result.Take(50), 300, false);
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
            if (message.From != null && !States.ContainsKey(message.From.Id))
            {
                States.Add(message.From.Id, UserState.Default);
            }

            if (message.Text == null || message.Type != MessageType.Text)
            {
                return;
            }
            if (message.From == null || message.Chat.Type == ChatType.Group || message.Chat.Type == ChatType.Supergroup || message.Chat.Type == ChatType.Channel)
            {
                await botClient.LeaveChatAsync(message.Chat);
                return;
            }
            if (message.Chat.Type == ChatType.Private && message.ViaBot == null && States[message.From.Id] == UserState.Default)
            {
                Resources.Culture = CultureInfo.GetCultureInfo(message.From.LanguageCode ?? "ru");
                message.Text = message.Text.Trim();
                var myself = await botClient.GetMeAsync();
                myself.Username ??= DefaultBotUsername;
                if (message.Text.IsCommandThrowed("/get_catalog", myself.Username))
                {
                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    CatalogNodes = FetchCatalogNodes();
                    await botClient.SendTextMessageAsync(message.Chat.Id, Resources.CatalogFetched, message.MessageThreadId, ParseMode.Html);
                }
                else if (message.Text.IsCommandThrowed("/start", myself.Username))
                {
                    var webApp = new WebAppInfo { Url = GlobalSettings.Instance.Url };
                    var buttons = new List<InlineKeyboardButton> { InlineKeyboardButton.WithWebApp(Resources.OpenWebapp, webApp), InlineKeyboardButton.WithUrl(Resources.OpenBrowser, GlobalSettings.Instance.Url) };
                    var keyboard = new InlineKeyboardMarkup(buttons);
                    await botClient.SendTextMessageAsync(message.Chat.Id, string.Format(Resources.Start, message.From.FirstName, (await botClient.GetMeAsync()).Username ?? DefaultBotUsername), message.MessageThreadId, ParseMode.Markdown, replyMarkup: keyboard);
                }
                else if (message.Text.IsCommandThrowed("/maps", myself.Username))
                {
                    if (message.Text.Equals("/maps", StringComparison.InvariantCultureIgnoreCase))
                    {
                        var maps = CatalogNodes.GetMaps(25);
                        var builder = new StringBuilder();
                        foreach (var map in maps)
                        {
                            builder.AppendFormat("[{0}]({1}) - [{2}]({3})" + "\n", map.Caption, map.FullUrl, map.Info.DescriptionCaption, map.Info.DescriptionLink);
                        }
                        await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), message.MessageThreadId, ParseMode.Markdown);
                    }
                }
                else if (message.Text.IsCommandThrowed("/admin", myself.Username))
                {
                    if (GlobalSettings.Instance.Admins.Contains(message.From.Id))
                    {
                        var builder = new StringBuilder();
                        builder.AppendLine(string.Format(Resources.NameParam, GlobalSettings.Instance.Name));
                        builder.AppendLine(string.Format(Resources.UrlParam, GlobalSettings.Instance.Url));
                        builder.AppendLine();
                        builder.AppendLine(string.Format(Resources.GeocoderParam, GlobalSettings.Instance.GeocoderUrl));
                        builder.AppendLine(string.Format(Resources.CadastreParam, GlobalSettings.Instance.CadastreUrl));

                        var buttons = new List<List<InlineKeyboardButton>>();
                        var firstLine = new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData(Resources.EditName, "editName"), InlineKeyboardButton.WithCallbackData(Resources.EditUrl, "editUrl") };
                        var secoundLine = new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData(Resources.EditGeocoderUrl, "editGeocoderUrl"), InlineKeyboardButton.WithCallbackData(Resources.EditCadastreUrl, "editCadastreUrl") };
                    }
                }
                else if (message.Text.StartsWith('/'))
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, string.Format(Resources.CommandNotFound, message.Text));
                }
                else if (message.Text.StartsWith('@'))
                {
                    return;
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, string.Format(Resources.StartedSearch, message.Text));

                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    #region Maps
                    var mapsResults = CatalogNodes.Search(message.Text).DistinctBy(x => x.Caption).Take(5);
                    var keyboard = null as InlineKeyboardMarkup;
                    if (!mapsResults.Any())
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, string.Format(Resources.MapsNotFound), replyToMessageId: message.MessageId);
                    }
                    else
                    {
                        var builder = new StringBuilder();
                        builder.AppendFormat(Resources.MapResultsStart + Environment.NewLine, message.Text, GlobalSettings.Instance.Name, GlobalSettings.Instance.Url);

                        if (mapsResults.Count() == 1)
                        {
                            builder.AppendLine($"- <a href=\"{mapsResults.First().GetUrl()}\">{mapsResults.First().Caption}</a>");
                            if (mapsResults.First().Info.DescriptionLink != "" && mapsResults.First().Info.DescriptionCaption != "")
                            {
                                builder.AppendLine(" - - " + mapsResults.First().Info.GetDescriptionText());
                            }
                            var info = new WebAppInfo()
                            {
                                Url = mapsResults.First().GetUrl()
                            };
                            keyboard = new InlineKeyboardMarkup(new InlineKeyboardButton[] { InlineKeyboardButton.WithUrl(Resources.OpenBrowser, mapsResults.First().GetUrl()),
                                                                                             InlineKeyboardButton.WithWebApp(Resources.OpenWebapp, info) });
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
                        await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), message.MessageThreadId, ParseMode.Html, replyMarkup: keyboard, replyToMessageId: message.MessageId);
                    }
                    #endregion
                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    #region Addresses
                    var addressResults = (await GeocoderProcessing.FindAddressCandidates(message.Text, GlobalSettings.Instance.GeocoderUrl)).Candidates.Where(x => x.Address.Length > 2).Take(5).ToList();
                    if (!addressResults.Any())
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, Resources.AddressesNotFound, replyToMessageId: message.MessageId);
                    }
                    else
                    {
                        var builder = new StringBuilder();
                        builder.AppendFormat(Resources.AddressResultsStart + Environment.NewLine, message.Text, GlobalSettings.Instance.Name, GlobalSettings.Instance.Url);
                        foreach (var result in addressResults)
                        {
                            builder.AppendLine($"- {result.Address}");
                        }
                        await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), message.MessageThreadId, ParseMode.Html, replyToMessageId: message.MessageId);
                    }
                    #endregion
                    await botClient.SendChatActionAsync(message.Chat.Id, ChatAction.Typing);
                    #region Cadastre
                    var cadastreResults = (await CadastreProcessing.Find(message.Text, GlobalSettings.Instance.CadastreUrl)).Results;
                    if (!cadastreResults.Any())
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, Resources.CadastreNotFound, replyToMessageId: message.MessageId);
                    }
                    else
                    {
                        var builder = new StringBuilder();
                        builder.AppendFormat(Resources.CadastreResultsStart + Environment.NewLine, message.Text, GlobalSettings.Instance.Name, GlobalSettings.Instance.Url);
                        foreach (var result in cadastreResults)
                        {
                            builder.AppendLine($"- {result.Attributes.Address} ({result.Attributes.Number})");
                        }
                        await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), message.MessageThreadId, ParseMode.Html, replyToMessageId: message.MessageId);
                    }
                    #endregion
                }
            }
            else if (message.Chat.Type == ChatType.Private && message.ViaBot == null && States[message.From.Id] == UserState.EditName)
            {
                var newName = message.Text.Trim();
                GlobalSettings.Instance.Name = newName;
                GlobalSettings.Instance.Save();
                await botClient.SendTextMessageAsync(chatId: message.Chat.Id, string.Format(Resources.NameSaved, newName), message.MessageThreadId);
            }
            else if (message.Chat.Type == ChatType.Private && message.ViaBot == null && States[message.From.Id] == UserState.EditUrl)
            {
                var newName = message.Text.Trim();
                GlobalSettings.Instance.Name = newName;
                GlobalSettings.Instance.Save();
                await botClient.SendTextMessageAsync(chatId: message.Chat.Id, string.Format(Resources.UrlSaved, newName), message.MessageThreadId);
            }
            else if (message.Chat.Type == ChatType.Private && message.ViaBot == null && States[message.From.Id] == UserState.EditGeocoder)
            {
                var newName = message.Text.Trim();
                GlobalSettings.Instance.Name = newName;
                GlobalSettings.Instance.Save();
                await botClient.SendTextMessageAsync(chatId: message.Chat.Id, string.Format(Resources.GeocoderSaved, newName), message.MessageThreadId);
            }
            else if (message.Chat.Type == ChatType.Private && message.ViaBot == null && States[message.From.Id] == UserState.EditCadastre)
            {
                var newName = message.Text.Trim();
                GlobalSettings.Instance.Name = newName;
                GlobalSettings.Instance.Save();
                await botClient.SendTextMessageAsync(chatId: message.Chat.Id, string.Format(Resources.CadastreSaved, newName), message.MessageThreadId);
            }
        }
        catch (Exception ex)
        {
            lock (ErrorsFile)
            {
                System.IO.File.AppendAllText("errors_textmessages.txt", ex.ToString() + Environment.NewLine);
            }
        }
    }
    #endregion

    public static async Task ProcessCallbackQuery(TelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        if (string.IsNullOrWhiteSpace(callbackQuery.Data) || callbackQuery.Message == null)
        {
            return;
        }
        if (GlobalSettings.Instance.Admins.Contains(callbackQuery.From.Id))
        {
            if (callbackQuery.Data.Equals("editName", StringComparison.InvariantCultureIgnoreCase))
            {
                if (!States.ContainsKey(callbackQuery.From.Id))
                {
                    States.Add(callbackQuery.From.Id, UserState.Default);
                }
                States[callbackQuery.From.Id] = UserState.EditName;
                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.SendName, callbackQuery.Message.MessageThreadId);
            }
            else if (callbackQuery.Data.Equals("editUrl", StringComparison.InvariantCultureIgnoreCase))
            {
                if (!States.ContainsKey(callbackQuery.From.Id))
                {
                    States.Add(callbackQuery.From.Id, UserState.Default);
                }
                States[callbackQuery.From.Id] = UserState.EditUrl;
                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.SendUrl, callbackQuery.Message.MessageThreadId);
            }
            else if (callbackQuery.Data.Equals("editGeocoder", StringComparison.InvariantCultureIgnoreCase))
            {
                if (!States.ContainsKey(callbackQuery.From.Id))
                {
                    States.Add(callbackQuery.From.Id, UserState.Default);
                }
                States[callbackQuery.From.Id] = UserState.EditGeocoder;
                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.SendGeocoder, callbackQuery.Message.MessageThreadId);
            }
            else if (callbackQuery.Data.Equals("editCadastre", StringComparison.InvariantCultureIgnoreCase))
            {
                if (!States.ContainsKey(callbackQuery.From.Id))
                {
                    States.Add(callbackQuery.From.Id, UserState.Default);
                }
                States[callbackQuery.From.Id] = UserState.EditCadastre;
                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.SendCadastre, callbackQuery.Message.MessageThreadId);
            }
        }
    }
}