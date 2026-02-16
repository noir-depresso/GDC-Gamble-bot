using System;
using System.Threading.Tasks;
using DiscordBot.Discord;

public class Program
{
    public static async Task Main()
    {
        var token = Environment.GetEnvironmentVariable("BOT_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Missing BOT_TOKEN environment variable.");
            return;
        }

        var bot = new BotClient(new BotConfig(token));
        await bot.StartAsync();

        await Task.Delay(-1);
    }
}