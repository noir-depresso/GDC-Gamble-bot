using System;
using System.Collections.Generic;
using System.Text;
using Game.Core.Cards;
using Game.Core.Effects;
using Game.Core.Models;

namespace Game.Core.Engine
{
    public class GameEngine
    {
        public Combatant Player { get; private set; }
        public Combatant Enemy { get; private set; }
        public GameState State { get; private set; }

        public List<CardDef> Hand { get; } = new();
        public int TurnNumber { get; private set; } = 1;

        private readonly Queue<CardDef> _deck = new();
        private readonly List<CardDef> _discard = new();
        private readonly EffectRunner _runner = new();

        public bool IsOver { get; private set; }

        public void StartNewGame()
        {
            Player = new Combatant("Player", 0, 100, false);
            Enemy = new Combatant("Enemy", 20, 120, true);
            State = new GameState { BasicIncome = 100, Money = 50, IncomeMultiplier = 1f };

            Hand.Clear();
            _deck.Clear();
            _discard.Clear();

            ShuffleIntoDeck(CardLibrary.StarterDeck());

            for (int i = 0; i < 5; i++) DrawCard();

            TurnNumber = 1;
            IsOver = false;
        }
        // Game.Core/Engine/GameEngine.cs
        public int FinalCost(CardDef card)
        {
            int stacks = State.GetStacks(EffectIds.BUY_LOW);
            stacks = Math.Min(stacks, 2);

            float mult = 1f - 0.10f * stacks;
            if (mult < 0f) mult = 0f;

            int cost = (int)MathF.Round(card.BaseCost * mult);

            // Optional rule: prevent accidental free cards unless baseCost is 0
            if (card.BaseCost > 0 && cost < 1) cost = 1;

            return cost;
        }

        public string PlayCard(int index)
        {
            if (IsOver) return "Game is over.";
            if (index < 0 || index >= Hand.Count) return "Invalid card index.";

            var card = Hand[index];
            int cost = FinalCost(card);

            if (State.Money < cost)
                return $"Not enough money for **{card.Name}**. Need {cost}, you have {State.Money}.";

            // pay
            State.Money -= cost;

            // move to discard
            Hand.RemoveAt(index);
            _discard.Add(card);

            var sb = new StringBuilder();
            sb.AppendLine($"You played **{card.Name}** (paid {cost}).");

            // resolve on-play effects
            var ctx = new EffectContext(Player, Enemy, State);
            var log = _runner.RunOnPlay(card, ctx);
            if (!string.IsNullOrWhiteSpace(log)) sb.AppendLine(log);

            if (Enemy.IsDead)
            {
                sb.AppendLine("✅ Enemy defeated. You win.");
                IsOver = true;
            }

            return sb.ToString().TrimEnd();
        }

        public string EndTurn()
        {
            if (IsOver) return "Game is over.";

            var sb = new StringBuilder();
            sb.AppendLine("**You ended your turn.**");

            // -------------------------
            // Enemy attacks (basic AI)
            // -------------------------
            int incoming = Enemy.Attack;

            // BEFORE TAKE DAMAGE (hedging etc.)
            var beforeCtx = new EffectContext(Player, Enemy, State) { PendingDamage = incoming };
            var beforeLog = _runner.FireTrigger(EffectTrigger.OnBeforeTakeDamage, beforeCtx);
            if (!string.IsNullOrWhiteSpace(beforeLog)) sb.AppendLine(beforeLog);

            int finalIncoming = beforeCtx.PendingDamage;
            Player.TakeDamage(finalIncoming);

            sb.AppendLine($"Enemy attacks for {finalIncoming}. Player HP: {Player.CurrentHealth}/{Player.MaxHealth}.");

            // AFTER TAKE DAMAGE (discredit reflect etc.)
            var afterCtx = new EffectContext(Player, Enemy, State) { PendingDamage = finalIncoming };
            var afterLog = _runner.FireTrigger(EffectTrigger.OnDamageTaken, afterCtx);
            if (!string.IsNullOrWhiteSpace(afterLog)) sb.AppendLine(afterLog);

            if (Player.IsDead)
            {
                sb.AppendLine("❌ You died. You lose.");
                IsOver = true;
                return sb.ToString().TrimEnd();
            }

            // -------------------------
            // End of round
            // -------------------------
            sb.AppendLine();
            sb.AppendLine("**End of Round**");

            // 1) Income payout
            int income = State.IncomeThisRound();
            State.Money += income;
            sb.AppendLine($"Income payout: +{income} (BI {State.BasicIncome} × x{State.IncomeMultiplier:0.00}).");

            // reset multiplier after payout
            State.IncomeMultiplier = 1f;

            // 2) Round-end investment ticks (Bank/SellHigh/Stocks etc.)
            var roundCtx = new EffectContext(Player, Enemy, State);
            var roundLog = _runner.FireTrigger(EffectTrigger.OnRoundEnd, roundCtx);
            if (!string.IsNullOrWhiteSpace(roundLog)) sb.AppendLine(roundLog);

            // 2.5) Turn-end triggers BEFORE durations tick (Discredit punish / Loan repayment)
            var turnEndLog = _runner.FireTrigger(EffectTrigger.OnTurnEnd, roundCtx);
            if (!string.IsNullOrWhiteSpace(turnEndLog)) sb.AppendLine(turnEndLog);

            // 3) Tick durations
            State.TickDurations();

            // 4) Start next player turn: draw 1
            TurnNumber++;
            DrawCard();

            sb.AppendLine();
            sb.AppendLine("**New turn started. You drew 1 card.**");

            return sb.ToString().TrimEnd();
        }

        private void DrawCard()
        {
            if (_deck.Count == 0)
            {
                if (_discard.Count == 0) return;
                ShuffleIntoDeck(_discard);
                _discard.Clear();
            }

            Hand.Add(_deck.Dequeue());
        }

        private void ShuffleIntoDeck(List<CardDef> cards)
        {
            var rng = new Random();
            var list = new List<CardDef>(cards);

            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }

            _deck.Clear();
            foreach (var c in list) _deck.Enqueue(c);
        }
    }
}