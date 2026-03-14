using System;
using System.IO;
using System.Threading.Tasks;
using DiscordBot.Application;
using DiscordBot.Discord;
using DiscordBot.Persistence;

// Application entry point. This stays intentionally small so startup wiring is easy to audit.
public class Program
{
    // Bootstraps configuration, persistence, the game service, and the Discord client.
    public static async Task Main()
    {
        // Prefer the current token name, but keep the legacy fallback so local setups do not break.
        var token = Environment.GetEnvironmentVariable("DISCORD_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
            token = Environment.GetEnvironmentVariable("BOT_TOKEN");

        if (string.IsNullOrWhiteSpace(token))
        {
            Console.WriteLine("Missing DISCORD_TOKEN environment variable.");
            return;
        }

        // Allow the database path to be overridden for local/dev/prod environments.
        var dbPath = Environment.GetEnvironmentVariable("GDC_DB_PATH");
        if (string.IsNullOrWhiteSpace(dbPath))
        {
            dbPath = Path.Combine("data", "gdc-gambling-bot.db");
        }

        var config = new BotConfig(token, dbPath);

        // Quick-run mode uses in-memory persistence while the ruleset is still changing frequently.
        IGameRepo repo = new InMemoryGameRepo();
        // IGameRepo repo = new SqliteGameRepo(config.DatabasePath);

        var lockProvider = new GameLockProvider();
        var gameService = new GameService(repo, lockProvider);
        var bot = new BotClient(config, gameService);

        await bot.StartAsync();

        // Keep the process alive after the socket client starts.
        await Task.Delay(-1);
    }
}
