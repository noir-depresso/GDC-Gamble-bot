using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DiscordBot.Persistence;
using Game.Core.Models;
using Game.Core.Sessions;

namespace DiscordBot.Application
{
    public class GameService
    {
        private const int DuelChallengeTimeoutMinutes = 10;
        private const int DuelWinsToFinish = 3;

        private sealed class DuelState
        {
            public ulong ChallengerUserId { get; init; }
            public ulong ChallengedUserId { get; init; }
            public DateTime CreatedAtUtc { get; init; }
            public bool IsActive { get; set; }
            public ulong CurrentTurnUserId { get; set; }
            public CharacterClass ChallengerClass { get; set; } = CharacterClass.Thief;
            public CharacterClass ChallengedClass { get; set; } = CharacterClass.Politician;
            public ulong? LastWinnerUserId { get; set; }
            public Dictionary<ulong, int> RoundWins { get; } = new();
        }

        private readonly IGameRepo _repo;
        private readonly GameLockProvider _lockProvider;
        private readonly ConcurrentDictionary<ulong, DuelState> _duelsByChannel = new();

        public GameService(IGameRepo repo, GameLockProvider lockProvider)
        {
            _repo = repo;
            _lockProvider = lockProvider;
        }

        public async Task<GameServiceResult> HandleCommandAsync(
            ulong channelId,
            ulong userId,
            string command,
            string[] args,
            string interactionId,
            CancellationToken ct = default)
        {
            var result = new GameServiceResult();
            string normalized = NormalizeCommand(command);

            if (normalized == "ping")
            {
                result.Messages.Add("pong");
                return result;
            }

            if (normalized == "help")
            {
                result.Messages.Add("**Commands**\n`!newgame` `!duel <@user|userId>` `!accept` `!decline` `!cancelduel` `!rematch` `!forfeit` `!kit <thief|politician>` `!bet <amount>` `!play <index>` `!end` `!choose <choiceId> <option>` `!useitem <index>` `!inspect <index>` `!job <cleaning|fetch|delivery|snake|coinflip>` `!nextcombat` `!status` `!hand` `!help`\nSlash: `/game create|kit|bet|play|end|choose|useitem|inspect|job|nextcombat|status|hand|help`");
                return result;
            }

            await using var guard = await _lockProvider.AcquireAsync(channelId, ct);

            var persisted = await _repo.LoadByChannelAsync(channelId, ct);
            if (persisted != null && persisted.LastInteractionId == interactionId)
            {
                result.Messages.Add("Duplicate command ignored.");
                return result;
            }

            GameSession? session = persisted == null ? null : GameSession.Restore(channelId, persisted.OwnerUserId, persisted.State);
            _duelsByChannel.TryGetValue(channelId, out var duel);

            if (duel != null && !duel.IsActive && DateTime.UtcNow - duel.CreatedAtUtc > TimeSpan.FromMinutes(DuelChallengeTimeoutMinutes))
            {
                _duelsByChannel.TryRemove(channelId, out _);
                duel = null;
            }

            if (normalized == "duel")
            {
                if (duel?.IsActive == true)
                {
                    result.Messages.Add("A duel is already active in this channel. Use `!forfeit` to end it first.");
                    return result;
                }

                if (args.Length < 1 || !TryParseUserId(args[0], out ulong challengedUserId))
                {
                    result.Messages.Add("Usage: `!duel <@user|userId>`");
                    return result;
                }

                if (challengedUserId == userId)
                {
                    result.Messages.Add("You cannot duel yourself.");
                    return result;
                }

                _duelsByChannel[channelId] = new DuelState
                {
                    ChallengerUserId = userId,
                    ChallengedUserId = challengedUserId,
                    IsActive = false,
                    CurrentTurnUserId = userId,
                    CreatedAtUtc = DateTime.UtcNow
                };

                result.Messages.Add($"Duel challenge sent: <@{userId}> vs <@{challengedUserId}>.");
                result.Messages.Add($"<@{challengedUserId}> type `!accept` (or `!decline`) within {DuelChallengeTimeoutMinutes} minutes.");
                return result;
            }

            if (normalized == "cancelduel")
            {
                if (duel == null || duel.IsActive)
                {
                    result.Messages.Add("No pending duel challenge to cancel.");
                    return result;
                }

                if (duel.ChallengerUserId != userId)
                {
                    result.Messages.Add("Only the challenger can cancel this pending duel.");
                    return result;
                }

                _duelsByChannel.TryRemove(channelId, out _);
                result.Messages.Add("Pending duel challenge canceled.");
                return result;
            }

            if (normalized == "decline")
            {
                if (duel == null || duel.IsActive)
                {
                    result.Messages.Add("No pending duel challenge in this channel.");
                    return result;
                }

                if (duel.ChallengedUserId != userId)
                {
                    result.Messages.Add($"Only <@{duel.ChallengedUserId}> can decline this duel.");
                    return result;
                }

                _duelsByChannel.TryRemove(channelId, out _);
                result.Messages.Add("Duel challenge declined.");
                return result;
            }

            if (normalized == "accept")
            {
                if (duel == null || duel.IsActive)
                {
                    result.Messages.Add("No pending duel challenge in this channel.");
                    return result;
                }

                if (duel.ChallengedUserId != userId)
                {
                    result.Messages.Add($"Only <@{duel.ChallengedUserId}> can accept this duel.");
                    return result;
                }

                session = new GameSession(channelId);
                duel.IsActive = true;
                duel.CurrentTurnUserId = duel.ChallengerUserId;
                duel.RoundWins[duel.ChallengerUserId] = 0;
                duel.RoundWins[duel.ChallengedUserId] = 0;

                session.StartNewGame(duel.CurrentTurnUserId);
                ApplyPreferredClassForUser(session, duel, duel.CurrentTurnUserId, result);

                await SaveSessionAsync(session, persisted?.GameId ?? Guid.NewGuid(), persisted?.CreatedAtUtc, interactionId, ct);

                result.Messages.Add($"Duel started: <@{duel.ChallengerUserId}> vs <@{duel.ChallengedUserId}> (first to {DuelWinsToFinish} rounds).");
                result.Messages.Add($"It is <@{duel.CurrentTurnUserId}>'s turn.");
                result.Messages.Add(session.IntroText());
                result.Messages.Add(session.StatusText());
                result.Messages.Add(session.HandText());
                return result;
            }

            if (normalized == "rematch")
            {
                if (duel == null || duel.IsActive)
                {
                    result.Messages.Add("No finished duel to rematch in this channel.");
                    return result;
                }

                bool participant = IsDuelParticipant(duel, userId);
                if (!participant)
                {
                    result.Messages.Add("Only previous duel participants can start a rematch.");
                    return result;
                }

                duel.IsActive = true;
                duel.RoundWins[duel.ChallengerUserId] = 0;
                duel.RoundWins[duel.ChallengedUserId] = 0;
                duel.CurrentTurnUserId = duel.LastWinnerUserId ?? duel.ChallengerUserId;

                session ??= new GameSession(channelId);
                session.StartNewGame(duel.CurrentTurnUserId);
                ApplyPreferredClassForUser(session, duel, duel.CurrentTurnUserId, result);

                await SaveSessionAsync(session, persisted?.GameId ?? Guid.NewGuid(), persisted?.CreatedAtUtc, interactionId, ct);

                result.Messages.Add($"Rematch started. It is <@{duel.CurrentTurnUserId}>'s turn.");
                result.Messages.Add(session.StatusText());
                result.Messages.Add(session.HandText());
                return result;
            }

            if (normalized == "forfeit")
            {
                if (duel == null || !duel.IsActive)
                {
                    result.Messages.Add("No active duel to forfeit.");
                    return result;
                }

                if (!IsDuelParticipant(duel, userId))
                {
                    result.Messages.Add("Only duel participants can forfeit this duel.");
                    return result;
                }

                ulong winner = OpponentUserId(duel, userId);
                duel.IsActive = false;
                duel.LastWinnerUserId = winner;
                result.Messages.Add($"<@{userId}> forfeited. Winner: <@{winner}>.");
                return result;
            }

            if (normalized == "newgame")
            {
                if (duel?.IsActive == true && !IsDuelParticipant(duel, userId))
                {
                    result.Messages.Add("Only duel participants can reset the duel game.");
                    return result;
                }

                session = new GameSession(channelId);
                session.StartNewGame(userId);

                if (duel?.IsActive == true)
                {
                    duel.CurrentTurnUserId = userId;
                    ApplyPreferredClassForUser(session, duel, userId, result);
                }

                await SaveSessionAsync(session, persisted?.GameId ?? Guid.NewGuid(), persisted?.CreatedAtUtc, interactionId, ct);

                result.Messages.Add(session.IntroText());
                result.Messages.Add(session.StatusText());
                result.Messages.Add(session.HandText());
                return result;
            }

            if (session == null || !session.IsInitialized || !session.HasGame)
            {
                result.Messages.Add("No active game in this channel. Use `!newgame` or `/game create`.");
                return result;
            }

            bool isDuelActive = duel?.IsActive == true;
            bool isParticipant = duel != null && IsDuelParticipant(duel, userId);
            bool isTurnLockedCommand =
                normalized == "bet" ||
                normalized == "choose" ||
                normalized == "useitem" ||
                normalized == "job" ||
                normalized == "nextcombat" ||
                normalized == "play" ||
                normalized == "end";

            if (isDuelActive)
            {
                if (!isParticipant)
                {
                    result.Messages.Add("Only duel participants can control this game.");
                    return result;
                }

                if (isTurnLockedCommand && duel != null && userId != duel.CurrentTurnUserId)
                {
                    result.Messages.Add($"Not your turn. It is currently <@{duel.CurrentTurnUserId}>'s turn.");
                    return result;
                }
            }
            else if (session.OwnerUserId != userId)
            {
                result.Messages.Add("This game is owned by someone else in this channel.");
                return result;
            }

            if (normalized == "kit")
            {
                if (args.Length < 1)
                {
                    result.Messages.Add("Usage: `!kit <thief|politician>`");
                    return result;
                }

                CharacterClass cls = ParseCharacterClass(args[0]);

                if (isDuelActive && duel != null)
                {
                    SetPreferredClassForUser(duel, userId, cls);

                    if (userId == duel.CurrentTurnUserId)
                    {
                        result.Messages.Add(session.SelectCharacter(cls));
                        await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                        result.Messages.Add(session.StatusText());
                        result.Messages.Add(session.HandText());
                    }
                    else
                    {
                        result.Messages.Add($"Saved your duel kit preference as **{cls}**. It will apply on your turn.");
                    }

                    return result;
                }

                result.Messages.Add(session.SelectCharacter(cls));
                await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                result.Messages.Add(session.StatusText());
                result.Messages.Add(session.HandText());
                return result;
            }

            if (normalized == "status")
            {
                result.Messages.Add(session.StatusText());
                if (isDuelActive && duel != null)
                {
                    int aWins = duel.RoundWins.GetValueOrDefault(duel.ChallengerUserId, 0);
                    int bWins = duel.RoundWins.GetValueOrDefault(duel.ChallengedUserId, 0);
                    result.Messages.Add($"Duel turn: <@{duel.CurrentTurnUserId}>. Score: <@{duel.ChallengerUserId}> {aWins} - {bWins} <@{duel.ChallengedUserId}>.");
                }
                return result;
            }

            if (normalized == "hand")
            {
                result.Messages.Add(session.HandText());
                return result;
            }

            if (normalized == "inspect")
            {
                if (args.Length < 1 || !int.TryParse(args[0], out int inspectIndex))
                {
                    result.Messages.Add("Usage: `!inspect <index>`");
                    return result;
                }

                result.Messages.Add(session.Inspect(inspectIndex));
                return result;
            }

            if (normalized == "bet")
            {
                if (args.Length < 1 || !int.TryParse(args[0], out int amount))
                {
                    result.Messages.Add("Usage: `!bet <amount>` or `/game bet <amount>`");
                    return result;
                }

                result.Messages.Add(session.Bet(amount));
                await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                result.Messages.Add(session.StatusText());
                return result;
            }

            if (normalized == "choose")
            {
                if (args.Length < 2)
                {
                    result.Messages.Add("Usage: `!choose <choiceId> <option>`");
                    return result;
                }

                result.Messages.Add(session.Choose(args[0], args[1]));
                await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                await ResolveDuelRoundIfNeededAsync(session, persisted, interactionId, ct, result, duel, userId);
                result.Messages.Add(session.StatusText());
                return result;
            }

            if (normalized == "useitem")
            {
                if (args.Length < 1 || !int.TryParse(args[0], out int itemIndex))
                {
                    result.Messages.Add("Usage: `!useitem <index>`");
                    return result;
                }

                result.Messages.Add(session.UseItem(itemIndex));
                await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                await ResolveDuelRoundIfNeededAsync(session, persisted, interactionId, ct, result, duel, userId);
                result.Messages.Add(session.StatusText());
                return result;
            }

            if (normalized == "job")
            {
                if (args.Length < 1)
                {
                    result.Messages.Add("Usage: `!job <cleaning|fetch|delivery|snake|coinflip>`");
                    return result;
                }

                result.Messages.Add(session.WorkJob(args[0]));
                await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                result.Messages.Add(session.StatusText());
                return result;
            }

            if (normalized == "nextcombat")
            {
                result.Messages.Add(session.StartNextCombat());
                await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                result.Messages.Add(session.StatusText());
                result.Messages.Add(session.HandText());
                return result;
            }

            if (normalized == "play")
            {
                if (args.Length < 1 || !int.TryParse(args[0], out int index))
                {
                    result.Messages.Add("Usage: `!play <index>` or `/game play <index>`");
                    return result;
                }

                result.Messages.Add(session.Play(index));
                await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                await ResolveDuelRoundIfNeededAsync(session, persisted, interactionId, ct, result, duel, userId);
                result.Messages.Add(session.StatusText());
                result.Messages.Add(session.HandText());
                return result;
            }

            if (normalized == "end")
            {
                string endText = session.EndTurn();
                result.Messages.Add(endText);

                if (isDuelActive && duel != null && endText.Contains("You ended your turn.", StringComparison.OrdinalIgnoreCase))
                {
                    duel.CurrentTurnUserId = OpponentUserId(duel, duel.CurrentTurnUserId);
                    ApplyPreferredClassForUser(session, duel, duel.CurrentTurnUserId, result);
                    result.Messages.Add($"Turn passes to <@{duel.CurrentTurnUserId}>.");
                }

                await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                await ResolveDuelRoundIfNeededAsync(session, persisted, interactionId, ct, result, duel, userId);
                result.Messages.Add(session.StatusText());
                result.Messages.Add(session.HandText());
                return result;
            }

            result.Messages.Add("Unknown command. Use `!help` or `/game help`.");
            return result;
        }

        private async Task ResolveDuelRoundIfNeededAsync(
            GameSession session,
            PersistedGame persisted,
            string interactionId,
            CancellationToken ct,
            GameServiceResult result,
            DuelState? duel,
            ulong actorUserId)
        {
            if (duel == null || !duel.IsActive) return;

            var state = session.GetStateSnapshot();
            if (state == null || !state.IsOver || state.Phase != GamePhase.CombatEnded) return;

            ulong winner = state.Player.IsDead ? OpponentUserId(duel, actorUserId) : actorUserId;
            duel.LastWinnerUserId = winner;
            duel.RoundWins[winner] = duel.RoundWins.GetValueOrDefault(winner, 0) + 1;

            int aWins = duel.RoundWins.GetValueOrDefault(duel.ChallengerUserId, 0);
            int bWins = duel.RoundWins.GetValueOrDefault(duel.ChallengedUserId, 0);
            result.Messages.Add($"Duel round winner: <@{winner}>. Score: <@{duel.ChallengerUserId}> {aWins} - {bWins} <@{duel.ChallengedUserId}>.");

            if (duel.RoundWins[winner] >= DuelWinsToFinish)
            {
                duel.IsActive = false;
                result.Messages.Add($"Duel complete. Winner: <@{winner}>.");
                return;
            }

            duel.CurrentTurnUserId = winner;
            session.StartNewGame(winner);
            ApplyPreferredClassForUser(session, duel, winner, result);
            await SaveSessionAsync(session, persisted.GameId, persisted.CreatedAtUtc, interactionId, ct);

            result.Messages.Add($"Next duel round started. It is <@{duel.CurrentTurnUserId}>'s turn.");
            result.Messages.Add(session.StatusText());
            result.Messages.Add(session.HandText());
        }

        private static string NormalizeCommand(string command)
        {
            return command.ToLowerInvariant() switch
            {
                "create" => "newgame",
                "next" => "nextcombat",
                "cancel" => "cancelduel",
                "resign" => "forfeit",
                _ => command.ToLowerInvariant()
            };
        }

        private static bool IsDuelParticipant(DuelState duel, ulong userId)
        {
            return userId == duel.ChallengerUserId || userId == duel.ChallengedUserId;
        }

        private static ulong OpponentUserId(DuelState duel, ulong userId)
        {
            return userId == duel.ChallengerUserId ? duel.ChallengedUserId : duel.ChallengerUserId;
        }

        private static CharacterClass ParseCharacterClass(string raw)
        {
            return string.Equals(raw, "politician", StringComparison.OrdinalIgnoreCase)
                ? CharacterClass.Politician
                : CharacterClass.Thief;
        }

        private static CharacterClass GetPreferredClassForUser(DuelState duel, ulong userId)
        {
            return userId == duel.ChallengerUserId ? duel.ChallengerClass : duel.ChallengedClass;
        }

        private static void SetPreferredClassForUser(DuelState duel, ulong userId, CharacterClass cls)
        {
            if (userId == duel.ChallengerUserId)
                duel.ChallengerClass = cls;
            else if (userId == duel.ChallengedUserId)
                duel.ChallengedClass = cls;
        }

        private static void ApplyPreferredClassForUser(GameSession session, DuelState duel, ulong userId, GameServiceResult result)
        {
            var cls = GetPreferredClassForUser(duel, userId);
            result.Messages.Add(session.SelectCharacter(cls));
        }

        private static bool TryParseUserId(string raw, out ulong userId)
        {
            userId = 0;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            string value = raw.Trim();
            if (ulong.TryParse(value, out userId))
                return true;

            if (value.StartsWith("<@", StringComparison.Ordinal) && value.EndsWith(">", StringComparison.Ordinal))
            {
                value = value[2..^1];
                if (value.StartsWith("!", StringComparison.Ordinal))
                    value = value[1..];
            }

            return ulong.TryParse(value, out userId);
        }

        private async Task SaveSessionAsync(GameSession session, Guid gameId, DateTime? createdAtUtc, string interactionId, CancellationToken ct)
        {
            var state = session.GetStateSnapshot();
            if (state == null) return;

            await _repo.SaveAsync(new PersistedGame
            {
                GameId = gameId,
                ChannelId = session.ChannelId,
                OwnerUserId = session.OwnerUserId,
                CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow,
                LastInteractionId = interactionId,
                State = state
            }, ct);
        }
    }
}
