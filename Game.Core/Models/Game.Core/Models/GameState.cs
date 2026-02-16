using System;
using System.Collections.Generic;

namespace Game.Core.Models
{
    public class GameState
    {
        public int BasicIncome { get; set; } = 100;
        public int Money { get; set; } = 50;

        // affects next income payout only
        public float IncomeMultiplier { get; set; } = 1f;

        private readonly Dictionary<string, StatusInstance> _statuses = new();
        private readonly Random _rng = new();

        public int GetStacks(string id) => _statuses.TryGetValue(id, out var s) ? s.Stacks : 0;

        public void AddStacks(string id, int add, int durationTurns)
        {
            if (!_statuses.ContainsKey(id))
                _statuses[id] = new StatusInstance(id, 0, durationTurns);

            _statuses[id].Stacks += add;

            if (durationTurns < 0)
            {
                _statuses[id].DurationTurns = -1;
            }
            else if (_statuses[id].DurationTurns != -1)
            {
                _statuses[id].DurationTurns = Math.Max(_statuses[id].DurationTurns, durationTurns);
            }
        }

        public void RemoveStatus(string id) => _statuses.Remove(id);

        public void TickDurations()
        {
            var keys = new List<string>(_statuses.Keys);
            foreach (var k in keys)
            {
                var s = _statuses[k];
                if (s.DurationTurns == -1) continue;

                s.DurationTurns -= 1;
                if (s.DurationTurns <= 0) _statuses.Remove(k);
            }
        }

        public int IncomeThisRound() => (int)MathF.Round(BasicIncome * IncomeMultiplier);

        public float Rand01() => (float)_rng.NextDouble();
        public float RandRange(float min, float max) => min + (max - min) * Rand01();
    
        public StatusInstance? GetStatus(string id) => _statuses.TryGetValue(id, out var s) ? s : null;
    }
}