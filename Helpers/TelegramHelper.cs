using Telegram.Bot.Types.Enums;

namespace CoGISBot.Telegram.Helpers;

public static class TelegramExtensions
{
    public static bool IsCommandThrowed(this string msg, string command, string botUsername)
    {
        if (string.IsNullOrWhiteSpace(msg))
        {
            return false;
        }
        return msg.StartsWith(command, StringComparison.InvariantCultureIgnoreCase);
    }
}
