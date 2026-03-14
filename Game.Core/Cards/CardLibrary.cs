using System;
using System.Collections.Generic;
using Game.Core.Effects;
using Game.Core.Effects.Implementations;
using Game.Core.Models;

namespace Game.Core.Cards
{
    /// <summary>
    /// Central lookup table for every card currently supported by the prototype.
    /// The engine asks this library for immutable card definitions by id whenever it needs card data.
    /// </summary>
    public static class CardLibrary
    {
        /// <summary>
        /// Returns the default 32-card starter deck before per-player deck composition overrides are applied.
        /// </summary>
        public static List<string> StarterDeckCardIds()
        {
            return new List<string>
            {
                "TROJAN", "TROJAN", "SOCIAL", "SOCIAL", "THERAPY", "NEURAL", "BANK", "SELLHIGH",
                "BUYLOW", "STOCKS", "LOAN", "COIN", "HEDGING", "ENCHANT", "GUARDDOWN", "SUTURE",
                "RAAN", "FIREWALL", "WANEWAX", "RELEASE", "WIRE", "HIREDGUN", "TRAUMA", "ROULETTE",
                "PROSTH", "CRYPTO", "REALESTATE", "EMP", "CHAOS", "DISCREDIT", "THERAPY", "TROJAN"
            };
        }

        /// <summary>
        /// Materializes a fresh card definition from its stable id.
        /// Fresh instances are fine here because card definitions are small and immutable in practice.
        /// </summary>
        public static CardDef GetById(string id)
        {
            return id switch
            {
                "BANK" => CreateBankAccount(),
                "LOAN" => CreateLoanShark(),
                "CRYPTO" => CreateCrypto(),
                "STOCKS" => CreateStocksAndBonds(),
                "BUYLOW" => CreateBuyLow(),
                "SELLHIGH" => CreateSellHigh(),
                "REALESTATE" => CreateRealEstate(),

                "RAAN" => CreateRaan(),
                "PERSUASION" => CreatePersuasion(),
                "DDOS" => CreateDdos(),
                "COIN" => CreateCoinFlip(),
                "HEDGING" => CreateHedging(),
                "RICHER" => CreateRichGetsRicher(),
                "NEURAL" => CreateNeuralNetworking(),
                "THERAPY" => CreateTherapy(),
                "PROSTH" => CreateProsthesis(),
                "WANEWAX" => CreateWaneAndWax(),
                "FIREWALL" => CreateFirewall(),
                "CHAOS" => CreateChaos(),
                "SCAPEGOAT" => CreateScapeGoat(),
                "ENCHANT" => CreateEnchantment(),
                "DISCREDIT" => CreateDiscredit(),
                "GUARDDOWN" => CreateGuardDown(),
                "RELEASE" => CreateReleaseFiles(),
                "BLOODY" => CreateBloodyBullets(),
                "POSTER" => CreatePropagandaPoster(),
                "SUTURE" => CreateSutureKit(),
                "SPIDER" => CreateSpiderAndroids(),

                "WIRE" => CreateWire(),
                "HACKERV" => CreateHackerV(),
                "TROJAN" => CreateTrojan(),
                "SOCIAL" => CreateSocialPressure(),
                "EXPOSE" => CreateExpose(),
                "CROWBAR" => CreateMetalCrowbar(),
                "EMP" => CreateEmp(),
                "TARGET" => CreateOnlineTargeting(),
                "HIREDGUN" => CreateHiredGun(),
                "TRAUMA" => CreateTraumaTeam(),

                "ROULETTE" => CreateRoulette(),
                "EYE" => CreateEyeForAnEye(),
                "SLICKTALK" => CreateSlickTalker(),

                _ => throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown card id")
            };
        }

        // Investment cards mainly shape the long-term combat economy.
        private static CardDef CreateBankAccount() => new("BANK", "Bank Account", CardType.Investment,
            "Increase 5% of basic income gained every round (stacks).", 25,
            new List<IEffect> { new AddStacksEffect(EffectIds.BANK_ACCOUNT, 1, -1) });

        private static CardDef CreateLoanShark() => new("LOAN", "Loan Shark", CardType.Investment,
            "Gain money now, repay with interest in 2 turns; 5% catastrophe risk.", 25,
            new List<IEffect> { new LoanSharkEffect() });

        private static CardDef CreateCrypto() => new("CRYPTO", "Crypto", CardType.Investment,
            "Next round gains +50% of this round earnings.", 100,
            new List<IEffect> { new CryptoEffect() });

        private static CardDef CreateStocksAndBonds() => new("STOCKS", "Stocks and Bonds", CardType.Investment,
            "Random -10% to +20% money each round per stack.", 50,
            new List<IEffect> { new StocksAndBondsStackEffect() });

        private static CardDef CreateBuyLow() => new("BUYLOW", "Buy Low", CardType.Investment,
            "Card costs are reduced by 20% per stack (max 2).", 150,
            new List<IEffect> { new BuyLowStackEffect() });

        private static CardDef CreateSellHigh() => new("SELLHIGH", "Sell High", CardType.Investment,
            "Gain 25% more money per turn (max 2 stacks).", 75,
            new List<IEffect> { new SellHighStackEffect() });

        private static CardDef CreateRealEstate() => new("REALESTATE", "Real Estate", CardType.Investment,
            "Increase base income by a random amount.", 50,
            new List<IEffect> { new RealEstateEffect() });

        // Medicate cards are mostly sustain, setup, and utility tools.
        private static CardDef CreateRaan() => new("RAAN", "Raan", CardType.Medicate,
            "After 2-3 turns, draw an extra card.", 50,
            new List<IEffect> { new RaanEffect() });

        private static CardDef CreatePersuasion() => new("PERSUASION", "Persuasion", CardType.Medicate,
            "Choose enemy attack debuff or player damage buff.", 100,
            new List<IEffect> { new PersuasionEffect() });

        private static CardDef CreateDdos() => new("DDOS", "D-dos", CardType.Medicate,
            "Choose to view enemy state or draw probability info.", 100,
            new List<IEffect> { new DdosEffect() });

        private static CardDef CreateCoinFlip() => new("COIN", "Coin Flip", CardType.Medicate,
            "Heads: double damage. Tails: half damage.", 75,
            new List<IEffect> { new CoinFlipDamageEffect(30) });

        private static CardDef CreateHedging() => new("HEDGING", "Hedging", CardType.Medicate,
            "Block 75% incoming damage next turn; lower income by 50%.", 75,
            new List<IEffect> { new HedgingEffect() });

        private static CardDef CreateRichGetsRicher() => new("RICHER", "The Rich Gets Richer", CardType.Medicate,
            "Formula ambiguous in spec; deferred.", 200,
            new List<IEffect> { new NoOpEffect("The Rich Gets Richer needs final formula; deferred.") });

        private static CardDef CreateNeuralNetworking() => new("NEURAL", "Neural-networking", CardType.Medicate,
            "Heal 30 HP.", 100,
            new List<IEffect> { new HealEffect(30) });

        private static CardDef CreateTherapy() => new("THERAPY", "Therapy", CardType.Medicate,
            "Heal 10 HP.", 50,
            new List<IEffect> { new HealEffect(10) });

        private static CardDef CreateProsthesis() => new("PROSTH", "Prosthesis", CardType.Medicate,
            "Heal 20% max HP and lose 10% permanent max HP.", 50,
            new List<IEffect> { new HealPercentAndReduceMaxHealthEffect(0.2f, 0.1f) });

        private static CardDef CreateWaneAndWax() => new("WANEWAX", "Wane and Wax", CardType.Medicate,
            "Next turn, heal 50% of damage dealt.", 100,
            new List<IEffect> { new WaneAndWaxEffect() });

        private static CardDef CreateFirewall() => new("FIREWALL", "Firewall", CardType.Medicate,
            "Reflect 25% of incoming damage this turn.", 100,
            new List<IEffect> { new FirewallEffect() });

        private static CardDef CreateChaos() => new("CHAOS", "Chaos", CardType.Medicate,
            "Reshuffle your hand with same card count.", 50,
            new List<IEffect> { new ChaosEffect() });

        private static CardDef CreateScapeGoat() => new("SCAPEGOAT", "ScapeGoat", CardType.Medicate,
            "Choose a card type to block for both sides.", 100,
            new List<IEffect> { new ScapeGoatEffect() });

        private static CardDef CreateEnchantment() => new("ENCHANT", "Enchantment", CardType.Medicate,
            "Increase damage dealt next turn by 30%.", 100,
            new List<IEffect> { new EnchantmentEffect() });

        private static CardDef CreateDiscredit() => new("DISCREDIT", "Discredit", CardType.Medicate,
            "If attacked this turn, block/reflect 50%; else self-damage 40%.", 50,
            new List<IEffect> { new DiscreditEffect() });

        private static CardDef CreateGuardDown() => new("GUARDDOWN", "Guard Down", CardType.Medicate,
            "Decrease enemy attack by 30%.", 75,
            new List<IEffect> { new GuardDownEffect() });

        private static CardDef CreateReleaseFiles() => new("RELEASE", "Release the Files", CardType.Medicate,
            "If at 20% HP or below, do double damage next turn.", 100,
            new List<IEffect> { new ReleaseFilesEffect() });

        private static CardDef CreateBloodyBullets() => new("BLOODY", "Bloody Bullets", CardType.Medicate,
            "Deals 30 damage.", 50,
            new List<IEffect> { new DealDamageEffect(30) });

        private static CardDef CreatePropagandaPoster() => new("POSTER", "Propaganda Poster", CardType.Medicate,
            "Fake hand overlay for peek interactions; deferred.", 125,
            new List<IEffect> { new NoOpEffect("Propaganda Poster needs hidden-hand/peek UI; deferred.") });

        private static CardDef CreateSutureKit() => new("SUTURE", "Suture Kit", CardType.Medicate,
            "Heal 25 and prevent damage for one turn.", 100,
            new List<IEffect> { new SutureKitEffect() });

        private static CardDef CreateSpiderAndroids() => new("SPIDER", "Spider Androids", CardType.Medicate,
            "Builds random item over 2 turns.", 225,
            new List<IEffect> { new SpiderAndroidsEffect() });

        // Bruiser cards are the direct-pressure tools that close fights.
        private static CardDef CreateWire() => new("WIRE", "The Wire", CardType.Bruiser,
            "Deal 10 damage. If this kills, gain extra draw after combat.", 50,
            new List<IEffect> { new DealDamageEffect(10) });

        private static CardDef CreateHackerV() => new("HACKERV", "Hacker V", CardType.Bruiser,
            "Decrease opponent basic income by 30% permanently.", 125,
            new List<IEffect> { new NoOpEffect("Hacker V requires opponent economy model; deferred in PvE prototype.") });

        private static CardDef CreateTrojan() => new("TROJAN", "Trojan Malware", CardType.Bruiser,
            "Deal 35 damage.", 100,
            new List<IEffect> { new DealDamageEffect(35) });

        private static CardDef CreateSocialPressure() => new("SOCIAL", "Social Pressure", CardType.Bruiser,
            "Base 10 + 10 per stack.", 50,
            new List<IEffect> { new SocialPressureEffect() });

        private static CardDef CreateExpose() => new("EXPOSE", "Expose", CardType.Bruiser,
            "Sacrifice one additional card to deal 75.", 75,
            new List<IEffect> { new ExposeEffect() });

        private static CardDef CreateMetalCrowbar() => new("CROWBAR", "Metal Crowbar", CardType.Bruiser,
            "Attack enemy cards for 15.", 50,
            new List<IEffect> { new NoOpEffect("Metal Crowbar conflicts with no-card-health combat model; deferred.") });

        private static CardDef CreateEmp() => new("EMP", "EMP", CardType.Bruiser,
            "Stun card effects for two turns.", 75,
            new List<IEffect> { new EmpEffect() });

        private static CardDef CreateOnlineTargeting() => new("TARGET", "Online Targeting", CardType.Bruiser,
            "Requires X DoT/rumor stacks.", 25,
            new List<IEffect> { new NoOpEffect("Online Targeting requires X + rumor/DoT systems; deferred.") });

        private static CardDef CreateHiredGun() => new("HIREDGUN", "Hired Gun", CardType.Bruiser,
            "Deal 20 and return to hand.", 150,
            new List<IEffect> { new HiredGunEffect() });

        private static CardDef CreateTraumaTeam() => new("TRAUMA", "Trauma Team", CardType.Bruiser,
            "If attacked next turn, deal 50 back; else heal 15.", 125,
            new List<IEffect> { new TraumaTeamEffect() });

        // Specials are mostly oddball or future-facing mechanics.
        private static CardDef CreateRoulette() => new("ROULETTE", "Roulette", CardType.Special,
            "Chance effect: self-damage/heal/enemy damage.", 50,
            new List<IEffect> { new RouletteEffect() });

        private static CardDef CreateEyeForAnEye() => new("EYE", "Eye for an Eye", CardType.Special,
            "Copy opponent attack.", 100,
            new List<IEffect> { new NoOpEffect("Eye for an Eye requires attack snapshot pipeline; deferred.") });

        private static CardDef CreateSlickTalker() => new("SLICKTALK", "Slick Talker", CardType.Special,
            "Free-play condition card.", 100,
            new List<IEffect> { new NoOpEffect("Slick Talker requires conditional free-play engine rule; deferred.") });
    }
}
