using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using DiscordBot.Application;

namespace DiscordBot.Discord
{
    /// <summary>
    /// Thin Discord transport layer that translates Discord events into game-service calls.
    /// It keeps Discord-specific concerns here so the rest of the game logic can stay platform-agnostic.
    /// </summary>
    public class BotClient
    {
        private readonly BotConfig _config;
        private readonly DiscordSocketClient _client;
        private readonly GameService _gameService;

        /// <summary>
        /// Builds the socket client and wires the handful of events the bot cares about.
        /// </summary>
        public BotClient(BotConfig config, GameService gameService)
        {
            _config = config;
            _gameService = gameService;

            // The bot only needs guild slash commands, normal messages, and message content for prefix commands.
            var socketConfig = new DiscordSocketConfig
            {
                GatewayIntents =
                    GatewayIntents.Guilds |
                    GatewayIntents.GuildMessages |
                    GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(socketConfig);
            _client.Log += msg => { Console.WriteLine(msg.ToString()); return Task.CompletedTask; };
            _client.Ready += OnReadyAsync;
            _client.MessageReceived += OnMessageReceivedAsync;
            _client.SlashCommandExecuted += OnSlashCommandExecutedAsync;
        }

        /// <summary>
        /// Logs in and starts the long-running Discord socket connection.
        /// </summary>
        public async Task StartAsync()
        {
            await _client.LoginAsync(TokenType.Bot, _config.Token);
            await _client.StartAsync();
        }

        /// <summary>
        /// Runs once Discord says the socket is ready, then refreshes slash commands.
        /// </summary>
        private async Task OnReadyAsync()
        {
            Console.WriteLine($"Online as {_client.CurrentUser}");
            await RegisterCommandsAsync();
        }

        /// <summary>
        /// Registers the current slash-command surface in one place so Discord stays in sync with the game service.
        /// </summary>
        private async Task RegisterCommandsAsync()
        {
            var game = new SlashCommandBuilder()
                .WithName("game")
                .WithDescription("Game commands")
                .AddOption(new SlashCommandOptionBuilder().WithName("create").WithDescription("Create/start a new game").WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder().WithName("kit").WithDescription("Select character kit").WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("name", ApplicationCommandOptionType.String, "thief or politician", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder().WithName("deck").WithDescription("Set deck composition counts").WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("bruiser", ApplicationCommandOptionType.Integer, "Bruiser cards count", isRequired: true)
                    .AddOption("medicate", ApplicationCommandOptionType.Integer, "Medicate cards count", isRequired: true)
                    .AddOption("investment", ApplicationCommandOptionType.Integer, "Investment cards count", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder().WithName("difficulty").WithDescription("Set game difficulty (1-5, default 3)").WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("level", ApplicationCommandOptionType.Integer, "Difficulty level", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder().WithName("bet").WithDescription("Place pre-fight bet").WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("amount", ApplicationCommandOptionType.Integer, "Bet amount (1-1000)", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder().WithName("choose").WithDescription("Resolve pending choice").WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("choice_id", ApplicationCommandOptionType.String, "Choice ID", isRequired: true)
                    .AddOption("option", ApplicationCommandOptionType.String, "Option value", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder().WithName("useitem").WithDescription("Use generated item").WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("index", ApplicationCommandOptionType.Integer, "Item index", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder().WithName("inspect").WithDescription("Inspect card in hand").WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("index", ApplicationCommandOptionType.Integer, "Card index in hand", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder().WithName("job").WithDescription("Run between-combat side job").WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("name", ApplicationCommandOptionType.String, "cleaning, fetch, delivery, snake, or coinflip", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder().WithName("nextcombat").WithDescription("Start next combat").WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder().WithName("status").WithDescription("Show game status").WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder().WithName("hand").WithDescription("Show your hand").WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder().WithName("play").WithDescription("Play a card by hand index").WithType(ApplicationCommandOptionType.SubCommand)
                    .AddOption("index", ApplicationCommandOptionType.Integer, "Card index in hand", isRequired: true))
                .AddOption(new SlashCommandOptionBuilder().WithName("end").WithDescription("End your turn").WithType(ApplicationCommandOptionType.SubCommand))
                .AddOption(new SlashCommandOptionBuilder().WithName("help").WithDescription("Show help").WithType(ApplicationCommandOptionType.SubCommand));

            await _client.BulkOverwriteGlobalApplicationCommandsAsync(new ApplicationCommandProperties[] { game.Build() });
        }

        /// <summary>
        /// Handles classic prefix commands and streams all resulting response lines back into the channel.
        /// </summary>
        private async Task OnMessageReceivedAsync(SocketMessage rawMsg)
        {
            if (rawMsg is not SocketUserMessage msg) return;
            if (msg.Author.IsBot) return;

            if (!CommandParser.TryParse(msg.Content, _config.Prefix, out string command, out string[] args))
                return;

            var result = await _gameService.HandleCommandAsync(msg.Channel.Id, msg.Author.Id, command, args, msg.Id.ToString());
            foreach (var line in result.Messages.Where(m => !string.IsNullOrWhiteSpace(m)))
                await msg.Channel.SendMessageAsync(line);
        }

        /// <summary>
        /// Normalizes slash subcommands into the same command/arg shape used by text commands.
        /// This keeps GameService as the single source of truth for behavior.
        /// </summary>
        private async Task OnSlashCommandExecutedAsync(SocketSlashCommand cmd)
        {
            if (!string.Equals(cmd.CommandName, "game", StringComparison.OrdinalIgnoreCase))
            {
                await cmd.RespondAsync("Unknown slash command.", ephemeral: true);
                return;
            }

            if (cmd.Data.Options.Count == 0)
            {
                await cmd.RespondAsync("Missing subcommand.", ephemeral: true);
                return;
            }

            var sub = (SocketSlashCommandDataOption)cmd.Data.Options.First();
            string command = sub.Name;
            string[] args = Array.Empty<string>();

            // Slash options are richer than prefix commands, so we flatten them into string args here.
            if (command is "play" or "bet" or "useitem" or "inspect" or "difficulty")
            {
                if (sub.Options.Count == 0)
                {
                    await cmd.RespondAsync("Missing numeric argument.", ephemeral: true);
                    return;
                }

                args = new[] { sub.Options.First().Value?.ToString() ?? string.Empty };
            }
            else if (command is "kit" or "job")
            {
                args = new[] { sub.Options.First().Value?.ToString() ?? string.Empty };
            }
            else if (command == "deck")
            {
                if (sub.Options.Count < 3)
                {
                    await cmd.RespondAsync("Need `bruiser`, `medicate`, and `investment`.", ephemeral: true);
                    return;
                }

                string bruiser = sub.Options.First(o => o.Name == "bruiser").Value?.ToString() ?? string.Empty;
                string medicate = sub.Options.First(o => o.Name == "medicate").Value?.ToString() ?? string.Empty;
                string investment = sub.Options.First(o => o.Name == "investment").Value?.ToString() ?? string.Empty;
                args = new[] { bruiser, medicate, investment };
            }
            else if (command == "choose")
            {
                if (sub.Options.Count < 2)
                {
                    await cmd.RespondAsync("Need `choice_id` and `option`.", ephemeral: true);
                    return;
                }

                string choiceId = sub.Options.First(o => o.Name == "choice_id").Value?.ToString() ?? string.Empty;
                string option = sub.Options.First(o => o.Name == "option").Value?.ToString() ?? string.Empty;
                args = new[] { choiceId, option };
            }

            if (cmd.ChannelId is not ulong channelId)
            {
                await cmd.RespondAsync("Unable to determine channel.", ephemeral: true);
                return;
            }

            var result = await _gameService.HandleCommandAsync(channelId, cmd.User.Id, command, args, cmd.Id.ToString());
            string response = string.Join("\n\n", result.Messages.Where(m => !string.IsNullOrWhiteSpace(m)));
            if (string.IsNullOrWhiteSpace(response)) response = "Done.";

            await cmd.RespondAsync(response);
        }
    }
}
