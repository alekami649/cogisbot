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
    static object StatsFile { get; set; } = new();
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
            else if (update.Type == UpdateType.ChosenInlineResult && update.ChosenInlineResult != null)
            {
                ProcessChosenInlineResult(update.ChosenInlineResult);
            }
            else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                await ProcessCallbackQuery(botClient, update.CallbackQuery);
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

    public static void ProcessChosenInlineResult(ChosenInlineResult chosenInlineResult)
    {
        var mapUrl = chosenInlineResult.ResultId;
        lock (StatsFile)
        {
            System.IO.File.AppendAllText("stats.log", mapUrl + "\n", Encoding.UTF8);
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
                        var secondLine = new List<InlineKeyboardButton> { InlineKeyboardButton.WithCallbackData(Resources.EditGeocoderUrl, "editGeocoder"), InlineKeyboardButton.WithCallbackData(Resources.EditCadastreUrl, "editCadastre") };
                        buttons.Add(firstLine);
                        buttons.Add(secondLine);

                        await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), message.MessageThreadId, replyMarkup: new InlineKeyboardMarkup(buttons));
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, Resources.InsufficientPermissions, message.MessageThreadId);
                    }
                }
                else if (message.Text.IsCommandThrowed("/stats", myself.Username))
                {
                    if (GlobalSettings.Instance.Admins.Contains(message.From.Id))
                    {
                        var builder = new StringBuilder();
                        var dictionary = new Dictionary<string, long>();

                        lock (StatsFile)
                        {
                            var lines = System.IO.File.ReadAllLines("stats.log", Encoding.UTF8);
                            dictionary = new Dictionary<string, long>(lines.GroupBy(x => x).ToDictionary(x => x.Key, x => x.LongCount()).OrderByDescending(x => x.Value));
                        }

                        builder.AppendLine(Resources.StatsStart);
                        var i = 1;

                        if (dictionary.Any())
                        {
                            foreach (var key in dictionary.Keys)
                            {
                                builder.AppendLine($"{i}. {key} ({dictionary[key]} {Resources.InlineClicksCount}).");
                                i++;
                            }
                        }
                        else
                        {
                            builder.AppendLine(Resources.NoInlineRequests);
                        }
                        await botClient.SendTextMessageAsync(message.Chat.Id, builder.ToString(), message.MessageThreadId);
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, Resources.InsufficientPermissions, message.MessageThreadId);
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
                if (GlobalSettings.Instance.Admins.Contains(message.From.Id))
                {
                    var newName = message.Text.Trim();
                    GlobalSettings.Instance.Name = newName;
                    GlobalSettings.Instance.Save();
                    States[message.From.Id] = UserState.Default;
                    await botClient.SendTextMessageAsync(chatId: message.Chat.Id, string.Format(Resources.NameSaved, newName), message.MessageThreadId);
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, Resources.InsufficientPermissions, message.MessageThreadId);
                }
            }
            else if (message.Chat.Type == ChatType.Private && message.ViaBot == null && States[message.From.Id] == UserState.EditUrl)
            {
                if (GlobalSettings.Instance.Admins.Contains(message.From.Id))
                {
                    var newUrl = message.Text.Trim();
                    GlobalSettings.Instance.Url = newUrl;
                    GlobalSettings.Instance.Save();
                    States[message.From.Id] = UserState.Default;
                    await botClient.SendTextMessageAsync(chatId: message.Chat.Id, string.Format(Resources.UrlSaved, newUrl), message.MessageThreadId);
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, Resources.InsufficientPermissions, message.MessageThreadId);
                }
            }
            else if (message.Chat.Type == ChatType.Private && message.ViaBot == null && States[message.From.Id] == UserState.EditGeocoder)
            {
                if (GlobalSettings.Instance.Admins.Contains(message.From.Id))
                {
                    var newGeocoder = message.Text.Trim();
                    GlobalSettings.Instance.GeocoderUrl = newGeocoder;
                    GlobalSettings.Instance.Save();
                    States[message.From.Id] = UserState.Default;
                    await botClient.SendTextMessageAsync(chatId: message.Chat.Id, string.Format(Resources.GeocoderSaved, newGeocoder), message.MessageThreadId);
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, Resources.InsufficientPermissions, message.MessageThreadId);
                }
            }
            else if (message.Chat.Type == ChatType.Private && message.ViaBot == null && States[message.From.Id] == UserState.EditCadastre)
            {
                if (GlobalSettings.Instance.Admins.Contains(message.From.Id))
                {
                    var newCadastre = message.Text.Trim();
                    GlobalSettings.Instance.CadastreUrl = newCadastre;
                    GlobalSettings.Instance.Save();
                    States[message.From.Id] = UserState.Default;
                    await botClient.SendTextMessageAsync(chatId: message.Chat.Id, string.Format(Resources.CadastreSaved, newCadastre), message.MessageThreadId);
                }
                else
                {
                    await botClient.SendTextMessageAsync(message.Chat.Id, Resources.InsufficientPermissions, message.MessageThreadId);
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
                if (GlobalSettings.Instance.Admins.Contains(callbackQuery.From.Id))
                {
                    if (!States.ContainsKey(callbackQuery.From.Id))
                    {
                        States.Add(callbackQuery.From.Id, UserState.Default);
                    }
                    States[callbackQuery.From.Id] = UserState.EditCadastre;
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.SendCadastre, callbackQuery.Message.MessageThreadId);
                }
                else
                {
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.InsufficientPermissions, callbackQuery.Message.MessageThreadId);
                }
            }
            else if (callbackQuery.Data.Equals("editUrl", StringComparison.InvariantCultureIgnoreCase))
            {
                if (GlobalSettings.Instance.Admins.Contains(callbackQuery.From.Id))
                {
                    if (!States.ContainsKey(callbackQuery.From.Id))
                    {
                        States.Add(callbackQuery.From.Id, UserState.Default);
                    }
                    States[callbackQuery.From.Id] = UserState.EditCadastre;
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.SendCadastre, callbackQuery.Message.MessageThreadId);
                }
                else
                {
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.InsufficientPermissions, callbackQuery.Message.MessageThreadId);
                }
            }
            else if (callbackQuery.Data.Equals("editGeocoder", StringComparison.InvariantCultureIgnoreCase))
            {
                if (GlobalSettings.Instance.Admins.Contains(callbackQuery.From.Id))
                {
                    if (!States.ContainsKey(callbackQuery.From.Id))
                    {
                        States.Add(callbackQuery.From.Id, UserState.Default);
                    }
                    States[callbackQuery.From.Id] = UserState.EditCadastre;
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.SendCadastre, callbackQuery.Message.MessageThreadId);
                }
                else
                {
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.InsufficientPermissions, callbackQuery.Message.MessageThreadId);
                }
            }
            else if (callbackQuery.Data.Equals("editCadastre", StringComparison.InvariantCultureIgnoreCase))
            {
                if (GlobalSettings.Instance.Admins.Contains(callbackQuery.From.Id))
                {
                    if (!States.ContainsKey(callbackQuery.From.Id))
                    {
                        States.Add(callbackQuery.From.Id, UserState.Default);
                    }
                    States[callbackQuery.From.Id] = UserState.EditCadastre;
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.SendCadastre, callbackQuery.Message.MessageThreadId);
                }
                else
                {
                    await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, Resources.InsufficientPermissions, callbackQuery.Message.MessageThreadId);
                }
            }
        }
    }
}