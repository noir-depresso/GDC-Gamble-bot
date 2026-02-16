using System.Text;
using Game.Core.Engine;
using Game.Core.Effects;
using Game.Core.Models;

namespace Game.Core.Sessions
{
    public class GameSession
    {
        public ulong ChannelId { get; }
        public ulong OwnerUserId { get; private set; }

        public bool HasGame => _engine != null && !_engine.IsOver;

        private GameEngine _engine;

        public GameSession(ulong channelId) => ChannelId = channelId;

        public void StartNewGame(ulong ownerUserId)
        {
            OwnerUserId = ownerUserId;
            _engine = new GameEngine();
            _engine.StartNewGame();
        }

        public string IntroText() =>
            "**New game started.**\n" +
            "Use `!hand`, `!play <index>`, `!end`, `!status`.";

        public string StatusText()
        {
             if (_engine == null) return "No game.";

            var p = _engine.Player;
            var e = _engine.Enemy;
            var s = _engine.State;

            int bank = s.GetStacks(EffectIds.BANK_ACCOUNT);
            int buyLow = s.GetStacks(EffectIds.BUY_LOW);
            int sellHigh = s.GetStacks(EffectIds.SELL_HIGH);
            int social = s.GetStacks(EffectIds.SOCIAL_PRESSURE);

            // --- Guaranteed next-round gains ---
            int incomeNext = s.IncomeThisRound(); // BI * multiplier
            int bankGain = (int)System.MathF.Round(s.BasicIncome * 0.05f * bank);
            int sellGain = (int)System.MathF.Round(s.BasicIncome * 0.10f * sellHigh); // match your runner percent
            int guaranteedGain = incomeNext + bankGain + sellGain;

            // --- Next attack multiplier preview (simple version) ---
            // If you later add enchant/double-damage statuses, check them here.
            float nextAttackMult = 1f;
            string multNotes = "None";

            // Example IDs you might add later:
            // if (s.GetStacks(EffectIds.ENCHANT_NEXT) > 0) { nextAttackMult *= 1.5f; multNotes = "✨ Enchant"; }
            // if (s.GetStacks(EffectIds.DOUBLE_DAMAGE_NEXT) > 0) { nextAttackMult *= 2f; multNotes = "🔥 Double"; }

            // Simple HP bars (10 blocks)
            string Bar(int cur, int max)
            {
                int filled = (int)System.MathF.Round(10f * cur / System.MathF.Max(1, max));
                filled = System.Math.Clamp(filled, 0, 10);
                return new string('█', filled) + new string('░', 10 - filled);
            }

            var sb = new StringBuilder();

            sb.AppendLine("**📌 STATUS**");
            sb.AppendLine($"🧍 **You:** `{p.CurrentHealth}/{p.MaxHealth}`  `{Bar(p.CurrentHealth, p.MaxHealth)}`");
            sb.AppendLine($"🤖 **Enemy:** `{e.CurrentHealth}/{e.MaxHealth}` `{Bar(e.CurrentHealth, e.MaxHealth)}`");
            sb.AppendLine();

            sb.AppendLine("**💰 ECONOMY**");
            sb.AppendLine($"💵 Money: **{s.Money}**");
            sb.AppendLine($"🏦 Basic Income (BI): **{s.BasicIncome}**");
            sb.AppendLine($"📈 Income Multiplier: **x{s.IncomeMultiplier:0.00}** (applies to next payout only)");
            sb.AppendLine();

            sb.AppendLine("**🔮 NEXT ROUND (GUARANTEED)**");
            sb.AppendLine($"✅ Income payout: `+{incomeNext}`");
            sb.AppendLine($"✅ Bank Account: `+{bankGain}` (5% BI × {bank})");
            sb.AppendLine($"✅ Sell High: `+{sellGain}` (10% BI × {sellHigh})");
            sb.AppendLine($"➡️ **Guaranteed total gain:** **+{guaranteedGain}**");
            sb.AppendLine($"🧾 **Money after next round (est):** **{s.Money + guaranteedGain}**");
            sb.AppendLine();

            sb.AppendLine("**⚔️ COMBAT READOUT**");
            sb.AppendLine($"🗓️ Turn: **{_engine.TurnNumber}**");
            sb.AppendLine($"🎯 Next attack multiplier: **x{nextAttackMult:0.00}** (`{multNotes}`)");
            sb.AppendLine();

            sb.AppendLine("**📚 STACKS**");
            sb.AppendLine($"🏦 Bank: `{bank}`   📉 Buy Low: `{buyLow}`   📈 Sell High: `{sellHigh}`   🗣️ Social: `{social}`");

            return sb.ToString();
        }

        public string HandText()
        {
            if (_engine == null) return "No game.";

            var sb = new StringBuilder();
            sb.AppendLine("**Hand**");
            for (int i = 0; i < _engine.Hand.Count; i++)
            {
                var c = _engine.Hand[i];
                int cost = _engine.FinalCost(c);
                sb.AppendLine($"`{i}` - {c.Name} (Cost: {cost})");
                sb.AppendLine($"     {c.Description}");
            }
            return sb.ToString();
        }

        public string Play(int index)
        {
            if (_engine == null) return "No game.";
            var result = _engine.PlayCard(index);
            return result + (HasGame ? "" : "\n(Game over)");
        }

        public string EndTurn()
        {
            if (_engine == null) return "No game.";
            var result = _engine.EndTurn();
            return result + (HasGame ? "" : "\n(Game over)");
        }
    }
}