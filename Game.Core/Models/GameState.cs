using System;
using System.Collections.Generic;

namespace Game.Core.Models
{
    public class GameState
    {
        public const int CurrentVersion = 4;

        public int GameStateVersion { get; set; } = CurrentVersion;
        public int TurnNumber { get; set; } = 1;
        public bool IsOver { get; set; }
        public GamePhase Phase { get; set; } = GamePhase.PreCombatPreview;

        public CharacterClass CharacterClass { get; set; } = CharacterClass.Thief;
        public List<string> LockedDeckCardIds { get; set; } = new();
        public List<string> FullDeckCardIds { get; set; } = new();

        public int BasicIncome { get; set; } = 100;
        public int Money { get; set; } = 500;
        public int Bits { get; set; } = 100;
        public int BitsPerTurn { get; set; } = 25;
        public int MaxHandSize { get; set; } = 6;

        public int BetAmount { get; set; }
        public int BitsSpentThisCombat { get; set; }
        public int BitsGainedThisCombat { get; set; }
        public int LastRoundMoneyGain { get; set; }
        public int PendingExtraDraws { get; set; }
        public bool WasAttackedThisTurn { get; set; }

        public string? BlockedCardType { get; set; }
        public int BlockedCardTypeTurns { get; set; }

        public PendingChoice? PendingChoice { get; set; }
        public List<string> GeneratedItems { get; set; } = new();

        public int LastCheckpointMoney { get; set; } = 500;
        public int LastCheckpointPlayerHp { get; set; } = 100;

        public int JobFatigue { get; set; }
        public int JobsCompleted { get; set; }
        public string? LastJobType { get; set; }

        public bool InDebt => Money < 0;
        public int DebtAmount => InDebt ? Math.Abs(Money) : 0;

        public Combatant Player { get; set; } = new("Player", 0, 100, false);
        public Combatant Enemy { get; set; } = new("Enemy", 20, 120, true);

        public List<string> DrawPileCardIds { get; set; } = new();
        public List<string> DiscardPileCardIds { get; set; } = new();
        public List<string> HandCardIds { get; set; } = new();
        public Dictionary<string, StatusInstance> Statuses { get; set; } = new();

        public int GetStacks(string id) => Statuses.TryGetValue(id, out var s) ? s.Stacks : 0;

        public void AddStacks(string id, int add, int durationTurns)
        {
            if (!Statuses.TryGetValue(id, out var status))
            {
                status = new StatusInstance(id, 0, durationTurns);
                Statuses[id] = status;
            }

            status.Stacks += add;

            if (durationTurns < 0)
            {
                status.DurationTurns = -1;
            }
            else if (status.DurationTurns != -1)
            {
                status.DurationTurns = Math.Max(status.DurationTurns, durationTurns);
            }
        }

        public void RemoveStatus(string id) => Statuses.Remove(id);

        public StatusInstance? GetStatus(string id) => Statuses.TryGetValue(id, out var s) ? s : null;

        public void TickDurations()
        {
            var keys = new List<string>(Statuses.Keys);
            foreach (var key in keys)
            {
                var status = Statuses[key];
                if (status.DurationTurns == -1) continue;

                status.DurationTurns -= 1;
                if (status.DurationTurns <= 0)
                    Statuses.Remove(key);
            }

            if (BlockedCardTypeTurns > 0)
            {
                BlockedCardTypeTurns--;
                if (BlockedCardTypeTurns == 0)
                    BlockedCardType = null;
            }
        }
    }
}
