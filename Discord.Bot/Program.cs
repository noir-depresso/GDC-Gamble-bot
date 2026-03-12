using System;
using System.IO;
using System.Threading.Tasks;
using DiscordBot.Application;
using DiscordBot.Discord;
using DiscordBot.Persistence;

public class Program
{
    public static async Task Main()
    {
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            token = Environment.GetEnvironmentVariable("BOT_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Missing DISCORD_TOKEN environment variable.");
            return;
        }

        var dbPath = Environment.GetEnvironmentVariable("GDC_DB_PATH");
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            dbPath = Path.Combine("data", "gdc-gambling-bot.db");
        }

        var config = new BotConfig(token, dbPath);
        // Quick-run mode: disable persistent storage for now.
        IGameRepo repo = new InMemoryGameRepo();
        // IGameRepo repo = new SqliteGameRepo(config.DatabasePath);
        var lockProvider = new GameLockProvider();
        var gameService = new GameService(repo, lockProvider);
        var bot = new BotClient(config, gameService);

        await bot.StartAsync();
        await Task.Delay(-1);
    }
}
