namespace CoGISBot.Telegram.Helpers;

public class UrlHelper
{
    public static string GetMapUrl(string mapUrl, bool hideControls = true)
    {
        mapUrl = mapUrl.Trim('#');
        mapUrl += $"?hideControls={hideControls}";
        return mapUrl;
    }
}
