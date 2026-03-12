using System.Linq;
using System.Text;
using Game.Core.Cards;
using Game.Core.Engine;
using Game.Core.Effects;
using Game.Core.Models;

namespace Game.Core.Sessions
{
    public class GameSession
    {
        public ulong ChannelId { get; }
        public ulong OwnerUserId { get; private set; }

        public bool HasGame => _engine != null;
        public bool InCombat => _engine != null && !_engine.State.IsOver;
        public bool IsInitialized => _engine != null;

        private GameEngine? _engine;

        public GameSession(ulong channelId)
        {
            ChannelId = channelId;
        }

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

        public GameState? GetStateSnapshot() => _engine?.State;

        public void StartNewGame(ulong ownerUserId)
        {
            OwnerUserId = ownerUserId;
            _engine = new GameEngine();
            _engine.StartNewGame();
        }

        public string IntroText() =>
            "**New game started.**\n" +
            "Use `!bet <amount>`, `!choose <choiceId> <option>`, `!useitem <index>`, `!inspect <index>`, `!hand`, `!play <index>`, `!end`, `!job <cleaning|fetch|delivery|snake|coinflip>`, `!nextcombat`, `!status`.";

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

            var sb = new StringBuilder();
            sb.AppendLine("**STATUS**");
            sb.AppendLine($"Phase: **{state.Phase}** Turn: **{state.TurnNumber}** Character: **{state.CharacterClass}**");
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
            if (!string.IsNullOrWhiteSpace(state.LastJobType))
                sb.AppendLine($"Last job: **{state.LastJobType}**");
            if (state.BlockedCardType != null)
                sb.AppendLine($"Blocked type: **{state.BlockedCardType}** ({state.BlockedCardTypeTurns} turns)");
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

        public string HandText()
        {
            if (_engine == null) return "No game.";

            var state = _engine.State;
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

        public string Inspect(int index)
        {
            if (_engine == null) return "No game.";

            var state = _engine.State;
            if (index < 0 || index >= state.HandCardIds.Count)
                return "Invalid card index.";

            var card = CardLibrary.GetById(state.HandCardIds[index]);
            int finalCost = _engine.FinalCost(state, card);

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

        public string Bet(int amount)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new PlaceBetAction(amount));
            return FormatUpdate(update);
        }

        public string StartNextCombat()
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new StartNextCombatAction());
            return FormatUpdate(update);
        }

        public string WorkJob(string jobType)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new WorkJobAction(jobType));
            return FormatUpdate(update);
        }

        public string SelectCharacter(CharacterClass characterClass)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new SelectCharacterAction(characterClass));
            return FormatUpdate(update);
        }

        public string Choose(string choiceId, string optionId)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new ChooseOptionAction(choiceId, optionId));
            return FormatUpdate(update);
        }

        public string UseItem(int itemIndex)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new UseGeneratedItemAction(itemIndex));
            return FormatUpdate(update);
        }

        public string Play(int index)
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new PlayCardAction(index));
            return FormatUpdate(update) + (InCombat ? "" : "\n(Combat ended)");
        }

        public string EndTurn()
        {
            if (_engine == null) return "No game.";
            var update = _engine.Apply(new EndTurnAction());
            return FormatUpdate(update) + (InCombat ? "" : "\n(Combat ended)");
        }

        private static string FormatUpdate(GameUpdate update)
        {
            if (!update.Success)
                return string.Join("\n", update.Errors);

            return string.Join("\n", update.Messages.Where(m => m != null));
        }
    }
}

