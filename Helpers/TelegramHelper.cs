namespace CoGISBot.Telegram.Helpers;

public static class TelegramExtensions
{
    public static bool IsCommandThrowed(this string msg, string command, string botUsername)
    {
        if (string.IsNullOrWhiteSpace(msg.TrimEnd()))
        {
            return false;
        }
        return msg.TrimEnd().StartsWith(command.TrimEnd(), StringComparison.InvariantCultureIgnoreCase);
    }
}
