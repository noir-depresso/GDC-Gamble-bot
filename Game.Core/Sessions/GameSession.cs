using System.Linq;
using System.Text;
using Game.Core.Cards;
using Game.Core.Engine;
using Game.Core.Effects;
using Game.Core.Models;

namespace Game.Core.Sessions
{
    // Thin session wrapper around the engine that is responsible for user-facing text formatting.
    public class GameSession
    {
        public ulong ChannelId { get; }
        public ulong OwnerUserId { get; private set; }

        public bool HasGame => _engine != null;
        public bool InCombat => _engine != null && !_engine.State.IsOver;
        public bool IsInitialized => _engine != null;

        private GameEngine? _engine;

        // A session is scoped to one Discord channel.
        public GameSession(ulong channelId)
        {
            ChannelId = channelId;
        }

        // Rebuilds a session wrapper around a previously persisted engine state.
        public static GameSession Restore(ulong channelId, ulong ownerUserId, GameState state)
        {
            var session = new GameSession(channelId)
            {
                OwnerUserId = ownerUserId,
                _engine = new GameEngine()
            };

            session._engine.LoadState(state);
            return session;
        }

        // Exposes the current snapshot for persistence and higher-level service decisions.
        public GameState? GetStateSnapshot() => _engine?.State;

        // Starts a new run while preserving runtime meta progression fields between runs.
        public void StartNewGame(ulong ownerUserId, DeckComposition? deckComposition = null)
        {
            OwnerUserId = ownerUserId;

            int metaCredits = _engine?.State.MetaCredits ?? 0;
            int lifetimeMetaCredits = _engine?.State.LifetimeMetaCredits ?? 0;
            int runsCompleted = _engine?.State.RunsCompleted ?? 0;
            int runsFailed = _engine?.State.RunsFailed ?? 0;

            _engine = new GameEngine();
            _engine.StartNewGame(deckComposition);

            _engine.State.MetaCredits = metaCredits;
            _engine.State.LifetimeMetaCredits = lifetimeMetaCredits;
            _engine.State.UnlockTier = GameState.UnlockTierFromLifetimeMetaCredits(lifetimeMetaCredits);
            _engine.State.RunsCompleted = runsCompleted;
            _engine.State.RunsFailed = runsFailed;
            _engine.State.CombatsWonThisRun = 0;
        }


        public int DifficultyLevel => _engine?.State.DifficultyLevel ?? GameState.DefaultDifficultyLevel;

        // Difficulty is applied directly to the active engine state so future turns use the new multipliers immediately.
        public string SetDifficulty(int difficultyLevel)
        {
            if (_engine == null) return "No game.";

            int clamped = GameState.ClampDifficultyLevel(difficultyLevel);
            _engine.State.DifficultyLevel = clamped;
            return $"Difficulty set to {clamped}.";
        }
        // Intro text is a compact reminder of the main commands the player will need right away.
        public string IntroText() =>
            "**New game started.**\n" +
            "Use `!difficulty <1-5>`, `!bet <amount>`, `!choose <choiceId> <option>`/`!packet <convert|credit|bank>`, `!useitem <index>`, `!inspect <index>`, `!hand`, `!play <index>`, `!end`, `!job <cleaning|fetch|delivery|snake|coinflip>`, `!nextcombat`, `!status`.";

        // Builds the main status screen shown after most commands.
        public string StatusText()
        {
            if (_engine == null) return "No game.";

            var state = _engine.State;
            var p = state.Player;
            var e = state.Enemy;

            int bank = state.GetStacks(EffectIds.BANK_ACCOUNT);
            int buyLow = state.GetStacks(EffectIds.BUY_LOW);
            int sellHigh = state.GetStacks(EffectIds.SELL_HIGH);
            int social = state.GetStacks(EffectIds.SOCIAL_PRESSURE);

            string Bar(int cur, int max)
            {
                int filled = (int)System.MathF.Round(10f * cur / System.MathF.Max(1, max));
                filled = System.Math.Clamp(filled, 0, 10);
                return new string('#', filled) + new string('-', 10 - filled);
            }

            // Status text is grouped by combat, economy, pending choice, and active status so the screen is scan-friendly.
            var sb = new StringBuilder();
            sb.AppendLine("**STATUS**");
            sb.AppendLine($"Phase: **{state.Phase}** Turn: **{state.TurnNumber}** Character: **{state.CharacterClass}** Difficulty: **{state.DifficultyLevel}**");
            sb.AppendLine($"Difficulty intent: {GameState.DifficultyIntentDescription(state.DifficultyLevel)}");
            sb.AppendLine($"Encounter: **{state.EncounterModifierLabel}** - {state.EncounterModifierSummary}");
            sb.AppendLine($"Difficulty multipliers: outgoing x{state.PlayerOutgoingDamageMultiplier:0.00}, incoming x{state.IncomingDamageMultiplier:0.00}, money x{state.RoundMoneyGainMultiplier:0.00}, AI tier {state.AiIntelligenceTier}");
            sb.AppendLine($"Final multipliers (with encounter): outgoing x{state.FinalOutgoingDamageMultiplier:0.00}, incoming x{state.FinalIncomingDamageMultiplier:0.00}, money x{state.FinalRoundMoneyGainMultiplier:0.00}");
            sb.AppendLine($"You: `{p.CurrentHealth}/{p.MaxHealth}` `{Bar(p.CurrentHealth, p.MaxHealth)}`");
            sb.AppendLine($"Enemy: `{e.CurrentHealth}/{e.MaxHealth}` `{Bar(e.CurrentHealth, e.MaxHealth)}`");
            sb.AppendLine();

            sb.AppendLine("**ECONOMY**");
            sb.AppendLine($"Money: **{state.Money}**");
            sb.AppendLine($"Bits: **{state.Bits}**");
            sb.AppendLine($"Basic Income: **{state.BasicIncome}**");
            sb.AppendLine($"Bits/turn: **{state.BitsPerTurn}**");
            sb.AppendLine($"Bet: **{state.BetAmount}**");
            sb.AppendLine($"Debt: **{(state.InDebt ? state.DebtAmount : 0)}**");
            sb.AppendLine($"Job fatigue: **{state.JobFatigue}** (jobs completed: {state.JobsCompleted})");
            sb.AppendLine($"Meta credits: **{state.MetaCredits}** (lifetime: {state.LifetimeMetaCredits}, unlock tier: {state.UnlockTier}, combats won this run: {state.CombatsWonThisRun})");
            sb.AppendLine($"Unlock perks: +{state.StartingBitsBonusFromUnlockTier} starting bits, +{state.PostCombatMoneyBonusFromUnlockTier} money on combat win.");
            if (state.NextUnlockTier > 0)
                sb.AppendLine($"Next unlock tier: {state.NextUnlockTier} in {state.CreditsToNextUnlock} lifetime meta credits.");
            else
                sb.AppendLine("Unlock track: max tier reached.");
            if (!string.IsNullOrWhiteSpace(state.LastJobType))
                sb.AppendLine($"Last job: **{state.LastJobType}**");
            if (state.BlockedCardType != null)
                sb.AppendLine($"Blocked type: **{state.BlockedCardType}** ({state.BlockedCardTypeTurns} turns)");
            if (state.NextCombatBonusBits > 0 || state.NextCombatEnemyAttackBonus > 0 || state.NextCombatBitsPenalty > 0 || state.PendingGrowthRiskLossPenalty > 0)
            {
                sb.AppendLine("Upcoming packet effects:");
                sb.AppendLine($"- Next combat bits: +{state.NextCombatBonusBits} / -{state.NextCombatBitsPenalty}");
                sb.AppendLine($"- Next combat enemy attack bonus: +{state.NextCombatEnemyAttackBonus}");
                sb.AppendLine($"- Defeat penalty risk: {state.PendingGrowthRiskLossPenalty}");
            }
            sb.AppendLine();

            if (state.PendingChoice != null)
            {
                sb.AppendLine("**PENDING CHOICE**");
                sb.AppendLine($"ID: `{state.PendingChoice.ChoiceId}` Type: `{state.PendingChoice.ChoiceType}`");
                sb.AppendLine($"Prompt: {state.PendingChoice.Prompt}");
                sb.AppendLine($"Options: {string.Join(", ", state.PendingChoice.Options)}");
                sb.AppendLine();
            }

            if (state.GeneratedItems.Count > 0)
            {
                sb.AppendLine("**GENERATED ITEMS**");
                for (int i = 0; i < state.GeneratedItems.Count; i++)
                    sb.AppendLine($"`{i}` - {state.GeneratedItems[i]}");
                sb.AppendLine();
            }

            sb.AppendLine("**STACKS**");
            sb.AppendLine($"Bank: `{bank}` Buy Low: `{buyLow}` Sell High: `{sellHigh}` Social: `{social}`");

            if (state.Statuses.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("**ACTIVE STATUSES**");
                foreach (var s in state.Statuses.Values.OrderBy(s => s.Id))
                {
                    var duration = s.DurationTurns < 0 ? "perm" : s.DurationTurns.ToString();
                    sb.AppendLine($"`{s.Id}` stacks={s.Stacks} turns={duration}");
                }
            }

            return sb.ToString();
        }

        // Shows the current hand with effective costs after modifiers are applied.
        public string HandText()
        {
            if (_engine == null) return "No game.";

            var state = _engine.State;
            // Status text is grouped by combat, economy, pending choice, and active status so the screen is scan-friendly.
            var sb = new StringBuilder();
            sb.AppendLine("**Hand**");

            for (int i = 0; i < state.HandCardIds.Count; i++)
            {
                var card = CardLibrary.GetById(state.HandCardIds[i]);
                int cost = _engine.FinalCost(state, card);
                sb.AppendLine($"`{i}` - {card.Name} [{card.Type}] (Cost: {cost}b)");
                sb.AppendLine($"     {card.Description}");
            }

            return sb.ToString();
        }

        // Gives deeper detail on one hand card without forcing the player to remember ids and effects.
        public string Inspect(int index)
        {
            if (_engine == null) return "No game.";

            var state = _engine.State;
            if (index < 0 || index >= state.HandCardIds.Count)
                return "Invalid card index.";

            var card = CardLibrary.GetById(state.HandCardIds[index]);
            int finalCost = _engine.FinalCost(state, card);

            // Status text is grouped by combat, economy, pending choice, and active status so the screen is scan-friendly.
            var sb = new StringBuilder();
            sb.AppendLine("**CARD INSPECT**");
            sb.AppendLine($"Name: **{card.Name}**");
            sb.AppendLine($"Id: `{card.Id}`");
            sb.AppendLine($"Type: `{card.Type}`");
            sb.AppendLine($"Base cost: `{card.BaseCostBits}` bits");
            sb.AppendLine($"Final cost now: `{finalCost}` bits");
            sb.AppendLine($"Description: {card.Description}");
            sb.AppendLine($"Effects: {card.Effects.Count}");

            return sb.ToString();
        }

        // Session helper that forwards the bet action to the engine and formats the result.
        public string Bet(int amount)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new PlaceBetAction(amount));
            return FormatUpdate(update);
        }

        // Starts the next combat and returns the engine's user-facing summary text.
        public string StartNextCombat()
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new StartNextCombatAction());
            return FormatUpdate(update);
        }

        // Runs a between-combat job through the engine and returns formatted output.
        public string WorkJob(string jobType)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new WorkJobAction(jobType));
            return FormatUpdate(update);
        }

        // Applies a class change through the engine when the current phase allows it.
        public string SelectCharacter(CharacterClass characterClass)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new SelectCharacterAction(characterClass));
            return FormatUpdate(update);
        }

        // Resolves a pending choice through the engine.
        public string Choose(string choiceId, string optionId)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new ChooseOptionAction(choiceId, optionId));
            return FormatUpdate(update);
        }

        // Uses a generated item and returns the resulting engine log.
        public string UseItem(int itemIndex)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new UseGeneratedItemAction(itemIndex));
            return FormatUpdate(update);
        }

        // Plays a hand card and appends a small combat-ended hint if that card finished the fight.
        public string Play(int index)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new PlayCardAction(index));
            return FormatUpdate(update) + (InCombat ? "" : "\n(Combat ended)");
        }

        // Ends the current turn and appends a combat-ended hint if the enemy turn resolved the fight.
        public string EndTurn()
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new EndTurnAction());
            return FormatUpdate(update) + (InCombat ? "" : "\n(Combat ended)");
        }

        // Normalizes engine updates into simple newline-delimited bot responses.
        private static string FormatUpdate(GameUpdate update)
        {
            if (!update.Success)
                return string.Join("\n", update.Errors);

            return string.Join("\n", update.Messages.Where(m => m != null));
        }
    }
}






