using Microsoft.AspNetCore.Connections.Features;
using Newtonsoft.Json;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace CoGISBot.Telegram;

public class PortalInfo
{
    public static string Name { get; set; } = System.IO.File.ReadAllText("brandname.txt");
    public static string Url { get; set; } = System.IO.File.ReadAllText("brandurl.txt");
}

public class CatalogNodesReponse
{
    [JsonProperty(nameof(CatalogNodes))]
    public List<CatalogNode> CatalogNodes { get; set; } = new List<CatalogNode>();

    public List<CatalogItem> GetMaps(int count)
    {
        return CatalogNodes.First().CatalogCategories.SelectMany(x => x.CatalogItems).Distinct().Take(count).ToList();
    }

    public List<CatalogItem> Search(string query)
    {
        var result = new List<CatalogItem>();
        foreach (var category in CatalogNodes.First().CatalogCategories)
        {
            result.AddRange(category.Search(query));
        }
        return result.Distinct().ToList();
    }
}

public class CatalogNode
{
    [JsonProperty("Items")]
    public List<CatalogCategory> CatalogCategories { get; set; } = new List<CatalogCategory>();

    public List<CatalogItem> GetMaps(int count)
    {
        return CatalogCategories.SelectMany(x => x.CatalogItems).Take(count).ToList();
    }

    public List<CatalogItem> Search(string query)
    {
        var result = new List<CatalogItem>();
        foreach (var category in CatalogCategories)
        {
            result.AddRange(category.Search(query));
        }
        return result;
    }
}

public class CatalogCategory
{
    [JsonProperty(nameof(Caption))]
    public string Caption { get; set; } = "";

    [JsonProperty("Items")]
    public List<CatalogItem> CatalogItems { get; set; } = new List<CatalogItem>();

    public List<CatalogItem> Search(string query)
    {
        var list = CatalogItems.Where(x => x.ValidateSearch(query));
        if (list.Any())
        {
            return list.ToList();
        }
        return new();
    }
}

public class CatalogItem
{
    [JsonProperty(nameof(Caption))]
    public string Caption { get; set; } = "";

    [JsonProperty("Name")]
    public string Url { get; set; } = "";

    [JsonProperty("LinkUrl")]
    public string FullUrl { get; set; } = "";

    [JsonProperty(nameof(Info))]
    public CatalogItemInfo Info { get; set; } = new CatalogItemInfo();

    [JsonProperty(nameof(Items))]
    public CatalogItem[]? Items { get; set; }

    [JsonIgnore]
    public PortalInfo PortalInfo { get; set; } = new PortalInfo();

    public string GetUrl()
    {
        if (!string.IsNullOrWhiteSpace(FullUrl))
        {
            return FullUrl;
        }
        else
        {
            return $"https://cogisdemo.dataeast.com/portal/{Url}/";
        }
    }

    public string GetText()
    {
        return $"Посмотрите <a href=\"{GetUrl()}\">{Caption}</a> в <a href=\"{PortalInfo.Url}\">{PortalInfo.Name}</a>!";
    }

    public InlineQueryResultArticle GetArticle()
    {
        //var info = new WebAppInfo()
        //{
        //    Url = GetUrl(),
        //};
        var keyboard = new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithUrl("Открыть в браузере", GetUrl()) });
        var result = new InlineQueryResultArticle(Url, Caption, new InputTextMessageContent(GetText())
        {
            DisableWebPagePreview = false,
            ParseMode = ParseMode.Html,
        })
        {
            ReplyMarkup = keyboard
        };
        return result;
    }

    public CatalogItem[]? Search(string query)
    {
        if (Items != null)
        {
            var subItems = Items.Where(x => x.Items != null).SelectMany(x => x.Search(query) ?? Array.Empty<CatalogItem>()).ToArray();
            return Items.Where(x => x.ValidateSearch(query)).Concat(subItems).Where(x => x.Items == null).ToArray();
        }
        return null;
    }

    public bool ValidateSearch(string query)
    {
        if (Info.Keywords != null)
        {
            return Caption.Contains(query, StringComparison.InvariantCultureIgnoreCase)
               || Url.Contains(query, StringComparison.InvariantCultureIgnoreCase)
               || Info.Keywords.Any(x => x.Contains(query, StringComparison.InvariantCultureIgnoreCase));
        }
        return Caption.Contains(query, StringComparison.InvariantCultureIgnoreCase)
               || Url.Contains(query, StringComparison.InvariantCultureIgnoreCase);
    }
}

public class CatalogItemInfo
{
    [JsonProperty(nameof(Keywords))]
    public string[] Keywords { get; set; } = Array.Empty<string>();

    [JsonProperty("PreviewImage")]
    public string ImageGuid { get; set; } = "";

    [JsonProperty("LinkControlCaption")]
    public string DescriptionCaption { get; set; } = "Описание";

    [JsonProperty("LinkControlUrl")]
    public string DescriptionLink { get; set; } = "";

    public string GetDescriptionText()
    {
        return $"{DescriptionCaption}: {DescriptionLink}";
    }

    public string GetImageUrl()
    {
        return $"https://cogisdemo.dataeast.com/portal/Images/{ImageGuid}.png";
    }

    public Uri GetImageUri()
    {
        return new(GetImageUrl());
    }
}