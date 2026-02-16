using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Game.Core.Engine;       // from Game.Core
using Game.Core.Sessions;     // from Game.Core

namespace DiscordBot.Discord
{
    public class BotClient
    {
        private readonly BotConfig _config;
        private readonly DiscordSocketClient _client;

        // One game per channel (easy demo).
        private readonly Dictionary<ulong, GameSession> _sessions = new();

        public BotClient(BotConfig config)
        {
            _config = config;

            var socketConfig = new DiscordSocketConfig
            {
                GatewayIntents =
                    GatewayIntents.Guilds |
                    GatewayIntents.GuildMessages |
                    GatewayIntents.MessageContent
            };

            _client = new DiscordSocketClient(socketConfig);

            _client.Log += msg => { Console.WriteLine(msg.ToString()); return Task.CompletedTask; };
            _client.Ready += () => { Console.WriteLine($"Online as {_client.CurrentUser}"); return Task.CompletedTask; };
            _client.MessageReceived += OnMessageReceivedAsync;
        }

        public async Task StartAsync()
        {
            await _client.LoginAsync(TokenType.Bot, _config.Token);
            await _client.StartAsync();
        }

        private async Task OnMessageReceivedAsync(SocketMessage rawMsg)
        {
            if (rawMsg is not SocketUserMessage msg) return;
            if (msg.Author.IsBot) return;
            if (!msg.Content.StartsWith(_config.Prefix)) return;

            string content = msg.Content.Substring(_config.Prefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(content)) return;

            string[] parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string cmd = parts[0].ToLowerInvariant();

            ulong channelId = msg.Channel.Id;

            _sessions.TryGetValue(channelId, out var session);

            // --- simple commands ---
            if (cmd == "ping")
            {
                await msg.Channel.SendMessageAsync("pong");
                return;
            }

            if (cmd == "help")
            {
                await msg.Channel.SendMessageAsync(
                    "**Commands**\n" +
                    "`!newgame` `!status` `!hand` `!play <index>` `!end` `!help`");
                return;
            }

            if (cmd == "newgame")
            {
                session = new GameSession(channelId);
                session.StartNewGame(msg.Author.Id);

                _sessions[channelId] = session;

                await msg.Channel.SendMessageAsync(session.IntroText());
                await msg.Channel.SendMessageAsync(session.StatusText());
                await msg.Channel.SendMessageAsync(session.HandText());
                return;
            }

            if (session == null || !session.HasGame)
            {
                await msg.Channel.SendMessageAsync("No active game in this channel. Use `!newgame`.");
                return;
            }

            // Lock demo to the owner
            if (session.OwnerUserId != msg.Author.Id)
            {
                await msg.Channel.SendMessageAsync("This game is owned by someone else in this channel.");
                return;
            }

            if (cmd == "status")
            {
                await msg.Channel.SendMessageAsync(session.StatusText());
                return;
            }

            if (cmd == "hand")
            {
                await msg.Channel.SendMessageAsync(session.HandText());
                return;
            }

            if (cmd == "play")
            {
                if (parts.Length < 2 || !int.TryParse(parts[1], out int index))
                {
                    await msg.Channel.SendMessageAsync("Usage: `!play <index>` (example: `!play 0`)");
                    return;
                }

                string result = session.Play(index);
                await msg.Channel.SendMessageAsync(result);

                if (!session.HasGame)
                {
                    await msg.Channel.SendMessageAsync("Game ended. Use `!newgame` to restart.");
                    return;
                }

                await msg.Channel.SendMessageAsync(session.StatusText());
                await msg.Channel.SendMessageAsync(session.HandText());
                return;
            }

            if (cmd == "end")
            {
                string result = session.EndTurn();
                await msg.Channel.SendMessageAsync(result);

                if (!session.HasGame)
                {
                    await msg.Channel.SendMessageAsync("Game ended. Use `!newgame` to restart.");
                    return;
                }

                await msg.Channel.SendMessageAsync(session.StatusText());
                await msg.Channel.SendMessageAsync(session.HandText());
                return;
            }

            await msg.Channel.SendMessageAsync("Unknown command. Use `!help`.");
        }
    }
}