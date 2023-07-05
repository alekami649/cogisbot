using Newtonsoft.Json;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InlineQueryResults;
using Telegram.Bot.Types.ReplyMarkups;

namespace CoGISBot.Telegram;

public class CatalogNodesReponse
{
    [JsonProperty(nameof(CatalogNodes))]
    public List<CatalogNode> CatalogNodes { get; set; } = new List<CatalogNode>();

    public List<CatalogItem> GetMaps(int count)
    {
        var endBlocks = CatalogNodes.SelectMany(x => x.CatalogCategories).SelectMany(x => CatalogCategory.GetEndblocks(x.CatalogItems)).ToList();
        return endBlocks.DistinctBy(x => x.FullUrl).Take(count).ToList();
    }

    public List<CatalogItem> Search(string query)
    {
        var result = new List<CatalogItem>();
        var endBlocks = CatalogNodes.SelectMany(x => x.CatalogCategories).SelectMany(x => CatalogCategory.GetEndblocks(x.CatalogItems)).ToList();
        result.AddRange(endBlocks.Where(x => x.ValidateSearch(query)));
        return result.DistinctBy(x => x.FullUrl).ToList();
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

    public static List<CatalogItem> GetEndblocks(List<CatalogItem> items)
    {
        var result = new List<CatalogItem>();
        foreach (var item in items)
        {
            if (item.Items != null && item.Items.Any())
            {
                result.AddRange(GetEndblocks(item.Items.ToList()));
            }
            else
            {
                result.Add(item);
            }
        }
        return result;
    }

    public List<CatalogItem> Search(string query)
    {
        var list = GetEndblocks(CatalogItems).Where(x => x.ValidateSearch(query));
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

    public string GetShareText()
    {
        return string.Format(Resources.MapShare, Caption, GetUrl(), GlobalSettings.Instance.Name, GlobalSettings.Instance.Url);
    }

    public InlineQueryResultArticle GetInlineQueryArticle()
    {
        var keyboard = new InlineKeyboardMarkup(new[] { InlineKeyboardButton.WithUrl(Resources.OpenBrowser, GetUrl()) });
        var result = new InlineQueryResultArticle(Url, Caption, new InputTextMessageContent(GetShareText())
        {
            DisableWebPagePreview = false,
            ParseMode = ParseMode.Html,
        })
        {
            ReplyMarkup = keyboard
        };
        return result;
    }

    public CatalogItem[]? SearchInside(string query)
    {
        if (Items != null)
        {
            var subItems = Items.Where(x => x.Items != null).SelectMany(x => x.SearchInside(query) ?? Array.Empty<CatalogItem>()).ToArray();
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
    public string DescriptionCaption { get; set; } = "Description";

    [JsonProperty("LinkControlUrl")]
    public string DescriptionLink { get; set; } = "";

    public string GetDescriptionText()
    {
        return $"<a href=\"{DescriptionLink}\">{DescriptionCaption ?? Resources.MapDescription}</a>";
    }

    public string GetImageUrl()
    {
        return $"{GlobalSettings.Instance.Url}/Images/{ImageGuid}.png";
    }

    public Uri GetImageUri()
    {
        return new(GetImageUrl());
    }
}