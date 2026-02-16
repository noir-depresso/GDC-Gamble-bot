using System.Collections.Generic;
using Game.Core.Effects;
using Game.Core.Effects.Implementations;

namespace Game.Core.Cards
{
    public static class CardLibrary
    {
        // This is what GameEngine calls:
        public static List<CardDef> StarterDeck()
        {
            var deck = new List<CardDef>();

            // --- Core test cards (already)
            deck.Add(CreateTrojan());
            deck.Add(CreateTrojan());
            deck.Add(CreateTherapy());
            deck.Add(CreateSocialPressure());
            deck.Add(CreateBuyLow());
            deck.Add(CreateSellHigh());
            deck.Add(CreateBankAccount());

            // --- Phase 1 timing test cards
            deck.Add(CreateEnchantment());
            deck.Add(CreateDiscredit());
            deck.Add(CreateLoanShark());
            deck.Add(CreateStocksAndBonds());

            // Add a few duplicates so you actually draw them
            deck.Add(CreateTrojan());
            deck.Add(CreateTherapy());
            deck.Add(CreateEnchantment());
            deck.Add(CreateStocksAndBonds());

            return deck;
        }

        // -----------------------------
        // Card factories
        // -----------------------------

        private static CardDef CreateTrojan()
            => new CardDef(
                id: "TROJAN",
                name: "Trojan Malware",
                description: "Deal 25 damage.",
                baseCost: 25,
                effects: new List<IEffect> { new DealDamageEffect(25) }
            );

        private static CardDef CreateTherapy()
            => new CardDef(
                id: "THERAPY",
                name: "Therapy",
                description: "Heal 30 HP.",
                baseCost: 25,
                effects: new List<IEffect> { new HealEffect(30) }
            );

        private static CardDef CreateSocialPressure()
            => new CardDef(
                id: "SOCIAL",
                name: "Social Pressure",
                description: "Gain 1 stack. Deal (10 + 5×stacks) damage.",
                baseCost: 20,
                effects: new List<IEffect> { new SocialPressureEffect() }
            );

        private static CardDef CreateBuyLow()
            => new CardDef(
                id: "BUYLOW",
                name: "Buy Low",
                description: "Gain 1 stack (max 2). Each stack reduces costs by 10%.",
                baseCost: 15,
                effects: new List<IEffect> { new BuyLowStackEffect() }
            );

        private static CardDef CreateSellHigh()
            => new CardDef(
                id: "SELLHIGH",
                name: "Sell High",
                description: "Gain 1 stack (max 2). End of round: +10% BI money per stack.",
                baseCost: 20,
                effects: new List<IEffect>
                {
                    new SellHighStackEffect(),
                    // payout happens in EffectRunner.OnRoundEnd (runner reads SELL_HIGH stacks)
                }
            );

        private static CardDef CreateBankAccount()
            => new CardDef(
                id: "BANK",
                name: "Bank Account",
                description: "Gain 1 stack. End of round: +5% BI money per stack.",
                baseCost: 30,
                effects: new List<IEffect>
                {
                    new AddStacksEffect(EffectIds.BANK_ACCOUNT, 1, -1),
                    // payout happens in EffectRunner.OnRoundEnd (runner reads BANK_ACCOUNT stacks)
                }
            );

        // -----------------------------
        // Phase 1 cards
        // -----------------------------

        private static CardDef CreateEnchantment()
            => new CardDef(
                id: "ENCHANT",
                name: "Enchantment",
                description: "Your next attack deals +50% damage.",
                baseCost: 15,
                effects: new List<IEffect> { new EnchantmentEffect() }
            );

        private static CardDef CreateDiscredit()
            => new CardDef(
                id: "DISCREDIT",
                name: "Discredit",
                description: "If attacked this turn, reflect 50%. Otherwise take 10 at turn end.",
                baseCost: 15,
                effects: new List<IEffect> { new DiscreditEffect() }
            );

        private static CardDef CreateLoanShark()
            => new CardDef(
                id: "LOAN",
                name: "Loan Shark",
                description: "Gain +100% BI now. Repay in 2 turns with interest or lose HP. 5% catastrophe.",
                baseCost: 20,
                effects: new List<IEffect> { new LoanSharkEffect() }
            );

        private static CardDef CreateStocksAndBonds()
            => new CardDef(
                id: "STOCKS",
                name: "Stocks & Bonds",
                description: "Gain 1 stack. End of round: random -20%..+20% money per stack.",
                baseCost: 25,
                effects: new List<IEffect> { new StocksAndBondsStackEffect() }
            );
    }
}