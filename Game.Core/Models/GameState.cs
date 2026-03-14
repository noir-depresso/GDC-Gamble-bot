using System;
using System.Collections.Generic;

namespace Game.Core.Models
{
    /// <summary>
    /// Serializable snapshot of the entire single-player run.
    /// Most engine methods mutate this object directly, so the comments here focus on what each cluster of fields represents.
    /// </summary>
    public class GameState
    {
        public const int CurrentVersion = 7;
        public const int MinDifficultyLevel = 1;
        public const int MaxDifficultyLevel = 5;
        public const int DefaultDifficultyLevel = 3;
        public const string EncounterNone = "none";
        public const string EncounterMarketCrash = "market_crash";
        public const string EncounterPowerSurge = "power_surge";
        public const string EncounterAudit = "audit";

        // Save-versioning and top-level run flow.
        public int GameStateVersion { get; set; } = CurrentVersion;
        public int TurnNumber { get; set; } = 1;
        public bool IsOver { get; set; }
        public GamePhase Phase { get; set; } = GamePhase.PreCombatPreview;

        // Player identity and deck structure.
        public CharacterClass CharacterClass { get; set; } = CharacterClass.Thief;
        public List<string> LockedDeckCardIds { get; set; } = new();
        public List<string> FullDeckCardIds { get; set; } = new();

        // Core combat/economy resources.
        public int BasicIncome { get; set; } = 100;
        public int Money { get; set; } = 500;
        public int Bits { get; set; } = 100;
        public int BitsPerTurn { get; set; } = 25;
        public int MaxHandSize { get; set; } = 6;
        public int DifficultyLevel { get; set; } = DefaultDifficultyLevel;

        // Per-combat bookkeeping used for rewards, betting, and delayed effects.
        public int BetAmount { get; set; }
        public int BitsSpentThisCombat { get; set; }
        public int BitsGainedThisCombat { get; set; }
        public int LastRoundMoneyGain { get; set; }
        public int PendingExtraDraws { get; set; }
        public bool WasAttackedThisTurn { get; set; }
        public int NextCombatBonusBits { get; set; }
        public int NextCombatEnemyAttackBonus { get; set; }
        public int NextCombatBitsPenalty { get; set; }
        public int PendingGrowthRiskLossPenalty { get; set; }
        public string ActiveEncounterModifier { get; set; } = EncounterNone;

        // Lightweight meta progression that persists per user profile in the bot layer.
        public int MetaCredits { get; set; }
        public int LifetimeMetaCredits { get; set; }
        public int UnlockTier { get; set; } = 1;
        public int CombatsWonThisRun { get; set; }
        public int RunsCompleted { get; set; }
        public int RunsFailed { get; set; }

        // Temporary global restrictions from choice cards.
        public string? BlockedCardType { get; set; }
        public int BlockedCardTypeTurns { get; set; }

        // Choice resolution and generated item hooks.
        public PendingChoice? PendingChoice { get; set; }
        public List<string> GeneratedItems { get; set; } = new();

        // Safe recovery points used when a player goes into debt between combats.
        public int LastCheckpointMoney { get; set; } = 500;
        public int LastCheckpointPlayerHp { get; set; } = 100;

        // Between-combat job pacing.
        public int JobFatigue { get; set; }
        public int JobsCompleted { get; set; }
        public string? LastJobType { get; set; }

        public bool InDebt => Money < 0;
        public int DebtAmount => InDebt ? Math.Abs(Money) : 0;

        // Current combatants.
        public Combatant Player { get; set; } = new("Player", 0, 100, false);
        public Combatant Enemy { get; set; } = new("Enemy", 20, 120, true);

        // Card zones and status effects.
        public List<string> DrawPileCardIds { get; set; } = new();
        public List<string> DiscardPileCardIds { get; set; } = new();
        public List<string> HandCardIds { get; set; } = new();
        public Dictionary<string, StatusInstance> Statuses { get; set; } = new();

        /// <summary>
        /// Normalizes caller input so difficulty math never runs outside the supported 1-5 range.
        /// </summary>
        public static int ClampDifficultyLevel(int difficultyLevel)
        {
            return Math.Clamp(difficultyLevel, MinDifficultyLevel, MaxDifficultyLevel);
        }

        /// <summary>
        /// Base outgoing player damage multiplier contributed by difficulty alone.
        /// </summary>
        public float PlayerOutgoingDamageMultiplier => ClampDifficultyLevel(DifficultyLevel) switch
        {
            1 => 1.30f,
            2 => 1.15f,
            3 => 1.00f,
            4 => 0.90f,
            5 => 0.80f,
            _ => 1.00f
        };

        /// <summary>
        /// Encounter-specific outgoing damage modifier layered on top of the difficulty multiplier.
        /// </summary>
        public float EncounterOutgoingDamageMultiplier => NormalizeEncounterModifier(ActiveEncounterModifier) switch
        {
            EncounterPowerSurge => 1.15f,
            EncounterAudit => 0.90f,
            _ => 1.00f
        };

        /// <summary>
        /// Base incoming damage multiplier contributed by difficulty alone.
        /// </summary>
        public float IncomingDamageMultiplier => ClampDifficultyLevel(DifficultyLevel) switch
        {
            1 => 0.75f,
            2 => 0.90f,
            3 => 1.00f,
            4 => 1.15f,
            5 => 1.30f,
            _ => 1.00f
        };

        /// <summary>
        /// Encounter-specific incoming damage modifier layered on top of the difficulty multiplier.
        /// </summary>
        public float EncounterIncomingDamageMultiplier => NormalizeEncounterModifier(ActiveEncounterModifier) switch
        {
            EncounterPowerSurge => 1.10f,
            _ => 1.00f
        };

        /// <summary>
        /// Placeholder hook for future enemy-brain upgrades. Right now it just tracks the chosen difficulty band.
        /// </summary>
        public int AiIntelligenceTier => ClampDifficultyLevel(DifficultyLevel);

        /// <summary>
        /// Base round-income multiplier contributed by difficulty alone.
        /// </summary>
        public float RoundMoneyGainMultiplier => ClampDifficultyLevel(DifficultyLevel) switch
        {
            1 => 1.30f,
            2 => 1.15f,
            3 => 1.00f,
            4 => 0.90f,
            5 => 0.80f,
            _ => 1.00f
        };

        /// <summary>
        /// Encounter-specific round-income modifier layered on top of the difficulty multiplier.
        /// </summary>
        public float EncounterRoundMoneyMultiplier => NormalizeEncounterModifier(ActiveEncounterModifier) switch
        {
            EncounterMarketCrash => 0.75f,
            EncounterPowerSurge => 0.95f,
            EncounterAudit => 1.10f,
            _ => 1.00f
        };

        // Final multipliers are what the engine should actually apply when resolving effects and turn income.
        public float FinalOutgoingDamageMultiplier => PlayerOutgoingDamageMultiplier * EncounterOutgoingDamageMultiplier;
        public float FinalIncomingDamageMultiplier => IncomingDamageMultiplier * EncounterIncomingDamageMultiplier;
        public float FinalRoundMoneyGainMultiplier => RoundMoneyGainMultiplier * EncounterRoundMoneyMultiplier;

        /// <summary>
        /// Applies the final outgoing damage multiplier while preserving a floor of 1 for positive damage packets.
        /// </summary>
        public int ScalePlayerOutgoingDamage(int rawDamage)
        {
            if (rawDamage <= 0) return 0;
            return Math.Max(1, (int)MathF.Round(rawDamage * FinalOutgoingDamageMultiplier));
        }

        /// <summary>
        /// Applies the final incoming damage multiplier while preserving a floor of 1 for positive damage packets.
        /// </summary>
        public int ScaleIncomingDamage(int rawDamage)
        {
            if (rawDamage <= 0) return 0;
            return Math.Max(1, (int)MathF.Round(rawDamage * FinalIncomingDamageMultiplier));
        }

        /// <summary>
        /// Applies the final round-income multiplier while preserving a floor of 1 for positive gains.
        /// </summary>
        public int ScaleRoundMoneyGain(int rawGain)
        {
            if (rawGain <= 0) return rawGain;
            return Math.Max(1, (int)MathF.Round(rawGain * FinalRoundMoneyGainMultiplier));
        }

        /// <summary>
        /// Human-readable shorthand for what each difficulty setting is supposed to feel like.
        /// </summary>
        public static string DifficultyIntentDescription(int difficultyLevel)
        {
            return ClampDifficultyLevel(difficultyLevel) switch
            {
                1 => "Learning mode: forgiving economy and damage.",
                2 => "Easy mode: experiment-friendly with light pressure.",
                3 => "Standard mode: baseline intended challenge.",
                4 => "Hard mode: mistakes are punished quickly.",
                5 => "Expert mode: high punishment, tight optimization expected.",
                _ => "Standard mode: baseline intended challenge."
            };
        }

        /// <summary>
        /// Per-tier starting-bits perk granted at the start of a new combat.
        /// </summary>
        public int StartingBitsBonusFromUnlockTier => UnlockTier switch
        {
            >= 4 => 20,
            3 => 12,
            2 => 6,
            _ => 0
        };

        /// <summary>
        /// Per-tier bonus money granted on combat win.
        /// </summary>
        public int PostCombatMoneyBonusFromUnlockTier => UnlockTier switch
        {
            >= 4 => 35,
            3 => 20,
            2 => 10,
            _ => 0
        };

        /// <summary>
        /// Converts lifetime meta-credit totals into the current unlock tier.
        /// </summary>
        public static int UnlockTierFromLifetimeMetaCredits(int lifetimeMetaCredits)
        {
            if (lifetimeMetaCredits >= 90) return 4;
            if (lifetimeMetaCredits >= 50) return 3;
            if (lifetimeMetaCredits >= 20) return 2;
            return 1;
        }

        /// <summary>
        /// The next unlock tier, or zero if the player is already capped.
        /// </summary>
        public int NextUnlockTier => UnlockTier switch
        {
            <= 1 => 2,
            2 => 3,
            3 => 4,
            _ => 0
        };

        /// <summary>
        /// Credits still required to reach the next tier threshold.
        /// </summary>
        public int CreditsToNextUnlock
        {
            get
            {
                int nextThreshold = NextUnlockTier switch
                {
                    2 => 20,
                    3 => 50,
                    4 => 90,
                    _ => 0
                };

                if (nextThreshold == 0) return 0;
                return Math.Max(0, nextThreshold - LifetimeMetaCredits);
            }
        }

        /// <summary>
        /// Sanitizes persisted or user-provided encounter ids into the known set.
        /// </summary>
        public static string NormalizeEncounterModifier(string? modifier)
        {
            string normalized = (modifier ?? string.Empty).Trim().ToLowerInvariant();
            return normalized switch
            {
                EncounterMarketCrash => EncounterMarketCrash,
                EncounterPowerSurge => EncounterPowerSurge,
                EncounterAudit => EncounterAudit,
                _ => EncounterNone
            };
        }

        /// <summary>
        /// Friendly encounter name for UI/status output.
        /// </summary>
        public string EncounterModifierLabel => NormalizeEncounterModifier(ActiveEncounterModifier) switch
        {
            EncounterMarketCrash => "Market Crash",
            EncounterPowerSurge => "Power Surge",
            EncounterAudit => "Audit",
            _ => "None"
        };

        /// <summary>
        /// One-line encounter summary for status/help output.
        /// </summary>
        public string EncounterModifierSummary => NormalizeEncounterModifier(ActiveEncounterModifier) switch
        {
            EncounterMarketCrash => "Round money gains reduced by 25%.",
            EncounterPowerSurge => "Outgoing damage +15%, incoming damage +10%, round money -5%.",
            EncounterAudit => "Outgoing damage -10%, round money gains +10%.",
            _ => "No global modifier this combat."
        };

        /// <summary>
        /// Convenience lookup used all over the engine and effect system.
        /// Missing statuses are treated as zero stacks.
        /// </summary>
        public int GetStacks(string id) => Statuses.TryGetValue(id, out var s) ? s.Stacks : 0;

        /// <summary>
        /// Adds or refreshes a status effect, preserving permanent statuses when duration is -1.
        /// </summary>
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

        /// <summary>
        /// Deletes a status outright.
        /// </summary>
        public void RemoveStatus(string id) => Statuses.Remove(id);

        /// <summary>
        /// Returns the full status record when callers need both stacks and duration.
        /// </summary>
        public StatusInstance? GetStatus(string id) => Statuses.TryGetValue(id, out var s) ? s : null;

        /// <summary>
        /// Ticks all temporary statuses and blocked-card restrictions by one turn.
        /// </summary>
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
