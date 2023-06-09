using CoGISBot.Telegram;
using System.Text;
using Telegram.Bot;

try
{
    var builder = WebApplication.CreateBuilder(args);

    var botClient = new TelegramBotClient(File.ReadAllText("telegram.secret"));
    builder.Services.AddSingleton(botClient);
    builder.Services.AddControllers().AddNewtonsoftJson();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    var app = builder.Build();

    if (!File.Exists("stats.log"))
    {
        File.WriteAllText("stats.log", "", Encoding.UTF8);
    }
    GlobalSettings.Instance = GlobalSettings.LoadOrCreate();
    GlobalSettings.Instance.Save();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
        botClient.StartReceiving<BotUpdateHandler>();
    }

    app.UseHttpsRedirection();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex)
{
    File.AppendAllText("errors_init.txt", ex.ToString() + Environment.NewLine);
}