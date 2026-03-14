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
    // Application-layer coordinator that turns Discord commands into validated engine actions.
    public class GameService
    {
        private const int DuelChallengeTimeoutMinutes = 10;
        private const int DuelWinsToFinish = 3;

        // Runtime-only duel metadata. This lives beside the command service because it is channel/session orchestration data.
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

        // Runtime meta profile cache used until persistent profile storage is introduced.
        private sealed record MetaProgress(int MetaCredits, int LifetimeMetaCredits, int UnlockTier, int RunsCompleted, int RunsFailed);

        private readonly IGameRepo _repo;
        private readonly GameLockProvider _lockProvider;
        private readonly ConcurrentDictionary<ulong, DuelState> _duelsByChannel = new();
        private readonly ConcurrentDictionary<ulong, DeckComposition> _deckPreferencesByUser = new();
        private readonly ConcurrentDictionary<ulong, int> _difficultyPreferencesByUser = new();
        private readonly ConcurrentDictionary<ulong, MetaProgress> _metaProgressByUser = new();

        // The service depends on persistence and per-channel locking, but keeps Discord types out of the engine.
        public GameService(IGameRepo repo, GameLockProvider lockProvider)
        {
            _repo = repo;
            _lockProvider = lockProvider;
        }

        // Main command pipeline: normalize -> load -> validate actor -> execute -> persist -> format replies.
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

            // Lightweight commands that do not need a loaded session return early.
            if (normalized == "help")
            {
                result.Messages.Add("**Commands**\n`!newgame` `!duel <@user|userId>` `!accept` `!decline` `!cancelduel` `!rematch` `!forfeit` `!kit <thief|politician>` `!deck <bruiser> <medicate> <investment>` `!difficulty <1-5>` `!bet <amount>` `!play <index>` `!end` `!choose <choiceId> <option>` `!packet <convert|credit|bank>` `!useitem <index>` `!inspect <index>` `!job <cleaning|fetch|delivery|snake|coinflip>` `!nextcombat` `!status` `!meta` `!hand` `!help`\nSlash: `/game create|kit|deck|difficulty|bet|play|end|choose|useitem|inspect|job|nextcombat|status|hand|help`");
                return result;
            }

            // Commands for the same channel are serialized so duplicate Discord events cannot interleave writes.
            await using var guard = await _lockProvider.AcquireAsync(channelId, ct);

            var persisted = await _repo.LoadByChannelAsync(channelId, ct);
            if (persisted != null && persisted.LastInteractionId == interactionId)
            {
                result.Messages.Add("Duplicate command ignored.");
                return result;
            }

            GameSession? session = persisted == null ? null : GameSession.Restore(channelId, persisted.OwnerUserId, persisted.State);
            if (session?.GetStateSnapshot() is GameState restoredState)
                SyncMetaProfileFromState(session.OwnerUserId, restoredState);
            _duelsByChannel.TryGetValue(channelId, out var duel);

            if (duel != null && !duel.IsActive && DateTime.UtcNow - duel.CreatedAtUtc > TimeSpan.FromMinutes(DuelChallengeTimeoutMinutes))
            {
                _duelsByChannel.TryRemove(channelId, out _);
                duel = null;
            }

            // Preference commands are allowed even when no active game exists yet.
            if (normalized == "deck")
            {
                if (args.Length < 3)
                {
                    if (_deckPreferencesByUser.TryGetValue(userId, out var current))
                    {
                        result.Messages.Add($"Current deck preference: bruiser={current.BruiserCount}, medicate={current.MedicateCount}, investment={current.InvestmentCount}, special={current.SpecialCount}.");
                    }
                    else
                    {
                        result.Messages.Add("No deck preference saved yet. Usage: `!deck <bruiser> <medicate> <investment>`.");
                    }

                    return result;
                }

                if (!TryParseDeckPreference(args, out var preference, out var parseError))
                {
                    result.Messages.Add(parseError);
                    return result;
                }

                _deckPreferencesByUser[userId] = preference;
                result.Messages.Add($"Saved deck preference for <@{userId}>: bruiser={preference.BruiserCount}, medicate={preference.MedicateCount}, investment={preference.InvestmentCount}, special={preference.SpecialCount}.");
                result.Messages.Add("Use `!newgame` to apply this immediately, or it will apply next time your game starts.");
                return result;
            }

            // Difficulty can be queried, saved for later, or applied to the live session.
            if (normalized == "difficulty")
            {
                int currentPreference = GetPreferredDifficultyForUser(userId);
                if (args.Length == 0)
                {
                    result.Messages.Add($"Current difficulty preference: **{currentPreference}** (default is {GameState.DefaultDifficultyLevel}).");
                    result.Messages.Add(GameState.DifficultyIntentDescription(currentPreference));
                    if (session != null && session.IsInitialized && session.HasGame)
                    {
                        result.Messages.Add($"Active game difficulty: **{session.DifficultyLevel}**.");
                        result.Messages.Add(GameState.DifficultyIntentDescription(session.DifficultyLevel));
                    }
                    return result;
                }

                if (!TryParseDifficulty(args[0], out int difficulty, out string difficultyError))
                {
                    result.Messages.Add(difficultyError);
                    return result;
                }

                _difficultyPreferencesByUser[userId] = difficulty;

                if (session != null && session.IsInitialized && session.HasGame)
                {
                    bool canApply = duel?.IsActive == true
                        ? IsDuelParticipant(duel, userId)
                        : session.OwnerUserId == userId;

                    if (canApply)
                    {
                        result.Messages.Add(session.SetDifficulty(difficulty));
                        result.Messages.Add(GameState.DifficultyIntentDescription(difficulty));
                        await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                        result.Messages.Add(session.StatusText());
                    }
                    else
                    {
                        result.Messages.Add($"Saved difficulty preference **{difficulty}**. It will apply when you start your own game.");
                    }
                }
                else
                {
                    result.Messages.Add($"Saved difficulty preference **{difficulty}**. Use `!newgame` to apply now.");
                }

                return result;
            }

            // Meta profile is exposed as a quick progression summary without requiring combat UI reading.
            if (normalized == "meta")
            {
                if (session != null && session.IsInitialized && session.HasGame && session.OwnerUserId == userId)
                {
                    var state = session.GetStateSnapshot();
                    if (state != null)
                    {
                        result.Messages.Add($"Meta profile: current={state.MetaCredits}, lifetime={state.LifetimeMetaCredits}, tier={state.UnlockTier}, runs={state.RunsCompleted} completed / {state.RunsFailed} failed.");
                        if (state.NextUnlockTier > 0)
                            result.Messages.Add($"Next tier {state.NextUnlockTier} in {state.CreditsToNextUnlock} lifetime meta credits.");
                        else
                            result.Messages.Add("Unlock track complete: max tier reached.");
                        return result;
                    }
                }

                if (_metaProgressByUser.TryGetValue(userId, out var metaProfile))
                {
                    result.Messages.Add($"Meta profile: current={metaProfile.MetaCredits}, lifetime={metaProfile.LifetimeMetaCredits}, tier={metaProfile.UnlockTier}, runs={metaProfile.RunsCompleted} completed / {metaProfile.RunsFailed} failed.");
                }
                else
                {
                    result.Messages.Add("Meta profile: no progress recorded yet. Play combats to earn meta credits.");
                }

                return result;
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

                StartNewGameForUser(session, duel.CurrentTurnUserId);
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
                StartNewGameForUser(session, duel.CurrentTurnUserId);
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

            // New game startup is also used as the reset path for solo runs and duel rounds.
            if (normalized == "newgame")
            {
                if (duel?.IsActive == true && !IsDuelParticipant(duel, userId))
                {
                    result.Messages.Add("Only duel participants can reset the duel game.");
                    return result;
                }

                session = new GameSession(channelId);
                StartNewGameForUser(session, userId);

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
            // Only a subset of commands should be restricted to the current duel turn owner.
            bool isTurnLockedCommand =
                normalized == "bet" ||
                normalized == "choose" ||
                normalized == "packet" ||
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


            if (normalized == "packet")
            {
                if (args.Length < 1)
                {
                    result.Messages.Add("Usage: `!packet <convert|credit|bank>`");
                    return result;
                }

                var state = session.GetStateSnapshot();
                if (state?.PendingChoice == null || !string.Equals(state.PendingChoice.ChoiceType, "INTERMISSION_PACKET", StringComparison.Ordinal))
                {
                    result.Messages.Add("No intermission packet is pending right now.");
                    return result;
                }

                result.Messages.Add(session.Choose(state.PendingChoice.ChoiceId, args[0]));
                await SaveSessionAsync(session, persisted!.GameId, persisted.CreatedAtUtc, interactionId, ct);
                result.Messages.Add(session.StatusText());
                return result;
            }
            if (normalized == "choose")
            {
                if (args.Length < 2)
                {
                    result.Messages.Add("Usage: `!choose <choiceId> <option>` `!packet <convert|credit|bank>`");
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

        // Duel mode treats each completed combat as one round in a best-of series.
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
            StartNewGameForUser(session, winner);
            ApplyPreferredClassForUser(session, duel, winner, result);
            await SaveSessionAsync(session, persisted.GameId, persisted.CreatedAtUtc, interactionId, ct);

            result.Messages.Add($"Next duel round started. It is <@{duel.CurrentTurnUserId}>'s turn.");
            result.Messages.Add(session.StatusText());
            result.Messages.Add(session.HandText());
        }

        // Small alias layer for ergonomic text commands.
        private static string NormalizeCommand(string command)
        {
            return command.ToLowerInvariant() switch
            {
                "create" => "newgame",
                "next" => "nextcombat",
                "cancel" => "cancelduel",
                "resign" => "forfeit",
                "pkt" => "packet",
                _ => command.ToLowerInvariant()
            };
        }

        // Utility guard used throughout duel validation paths.
        private static bool IsDuelParticipant(DuelState duel, ulong userId)
        {
            return userId == duel.ChallengerUserId || userId == duel.ChallengedUserId;
        }

        // Resolves the other player in a two-person duel.
        private static ulong OpponentUserId(DuelState duel, ulong userId)
        {
            return userId == duel.ChallengerUserId ? duel.ChallengedUserId : duel.ChallengerUserId;
        }

        // Character parsing stays forgiving so minor input differences do not block play.
        private static CharacterClass ParseCharacterClass(string raw)
        {
            return string.Equals(raw, "politician", StringComparison.OrdinalIgnoreCase)
                ? CharacterClass.Politician
                : CharacterClass.Thief;
        }

        // Duel participants each keep their own preferred class between rounds.
        private static CharacterClass GetPreferredClassForUser(DuelState duel, ulong userId)
        {
            return userId == duel.ChallengerUserId ? duel.ChallengerClass : duel.ChallengedClass;
        }

        // Writes a duel participant's preferred class back into the duel state.
        private static void SetPreferredClassForUser(DuelState duel, ulong userId, CharacterClass cls)
        {
            if (userId == duel.ChallengerUserId)
                duel.ChallengerClass = cls;
            else if (userId == duel.ChallengedUserId)
                duel.ChallengedClass = cls;
        }

        // Applies the stored class preference to the active session and forwards the engine message to the user.
        private static void ApplyPreferredClassForUser(GameSession session, DuelState duel, ulong userId, GameServiceResult result)
        {
            var cls = GetPreferredClassForUser(duel, userId);
            result.Messages.Add(session.SelectCharacter(cls));
        }

        // Centralized run startup so difficulty, deck preference, and meta profile are always applied together.
        private void StartNewGameForUser(GameSession session, ulong userId)
        {
            _deckPreferencesByUser.TryGetValue(userId, out var deckPreference);
            session.StartNewGame(userId, deckPreference);
            session.SetDifficulty(GetPreferredDifficultyForUser(userId));
            ApplyMetaProfileToSession(userId, session);
        }



        // Reads the saved difficulty preference or falls back to the global default.
        private int GetPreferredDifficultyForUser(ulong userId)
        {
            return _difficultyPreferencesByUser.TryGetValue(userId, out int difficulty)
                ? GameState.ClampDifficultyLevel(difficulty)
                : GameState.DefaultDifficultyLevel;
        }

        // Rehydrates runtime meta progression into a newly created session snapshot.
        private void ApplyMetaProfileToSession(ulong userId, GameSession session)
        {
            if (!_metaProgressByUser.TryGetValue(userId, out var profile)) return;
            var state = session.GetStateSnapshot();
            if (state == null) return;


            state.MetaCredits = Math.Max(0, profile.MetaCredits);
            state.LifetimeMetaCredits = Math.Max(0, profile.LifetimeMetaCredits);
            state.UnlockTier = GameState.UnlockTierFromLifetimeMetaCredits(state.LifetimeMetaCredits);
            state.RunsCompleted = Math.Max(0, profile.RunsCompleted);
            state.RunsFailed = Math.Max(0, profile.RunsFailed);
        }

        // Persists the latest meta-profile fields in memory whenever a session snapshot changes.
        private void SyncMetaProfileFromState(ulong userId, GameState state)
        {
            _metaProgressByUser[userId] = new MetaProgress(
                Math.Max(0, state.MetaCredits),
                Math.Max(0, state.LifetimeMetaCredits),
                GameState.UnlockTierFromLifetimeMetaCredits(state.LifetimeMetaCredits),
                Math.Max(0, state.RunsCompleted),
                Math.Max(0, state.RunsFailed));
        }

        // Validates the public 1-5 difficulty command contract.
        private static bool TryParseDifficulty(string raw, out int difficulty, out string error)
        {
            error = string.Empty;
            if (!int.TryParse(raw, out difficulty))
            {
                error = "Usage: `!difficulty <1-5>`.";
                return false;
            }

            if (difficulty < GameState.MinDifficultyLevel || difficulty > GameState.MaxDifficultyLevel)
            {
                error = $"Difficulty must be between {GameState.MinDifficultyLevel} and {GameState.MaxDifficultyLevel}.";
                return false;
            }

            return true;
        }
        // Parses the deck-type preference tuple used by !deck and /game deck.
        private static bool TryParseDeckPreference(string[] args, out DeckComposition preference, out string error)
        {
            preference = new DeckComposition(0, 0, 0);
            error = string.Empty;

            if (!int.TryParse(args[0], out int bruiser) || !int.TryParse(args[1], out int medicate) || !int.TryParse(args[2], out int investment))
            {
                error = "Usage: `!deck <bruiser> <medicate> <investment>` (all values must be integers).";
                return false;
            }

            try
            {
                preference = new DeckComposition(bruiser, medicate, investment);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                error = "Invalid deck counts. Values must be >= 0 and sum to 32 or less.";
                return false;
            }
        }

        // Accepts either raw ids or Discord mention syntax for duel commands.
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

        // Saves the latest session snapshot and updates the in-memory meta profile cache in one place.
        private async Task SaveSessionAsync(GameSession session, Guid gameId, DateTime? createdAtUtc, string interactionId, CancellationToken ct)
        {
            var state = session.GetStateSnapshot();
            if (state == null) return;

            SyncMetaProfileFromState(session.OwnerUserId, state);
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








