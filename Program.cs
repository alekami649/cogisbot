using CoGISBot.Telegram;
using CoGISBot.Telegram.Processing;
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