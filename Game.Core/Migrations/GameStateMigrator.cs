using System;
using Game.Core.Models;

namespace Game.Core.Migrations
{
    /// <summary>
    /// Applies additive save migrations so older serialized games can still load after schema changes.
    /// </summary>
    public class GameStateMigrator : IGameStateMigrator
    {
        /// <summary>
        /// Steps a loaded state forward version-by-version until it matches the current schema.
        /// </summary>
        public GameState Migrate(GameState state, int loadedVersion)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            int version = loadedVersion;
            while (version < GameState.CurrentVersion)
            {
                state = version switch
                {
                    1 => MigrateV1ToV2(state),
                    2 => MigrateV2ToV3(state),
                    3 => MigrateV3ToV4(state),
                    4 => MigrateV4ToV5(state),
                    5 => MigrateV5ToV6(state),
                    6 => MigrateV6ToV7(state),
                    _ => state
                };
                version++;
            }

            state.GameStateVersion = GameState.CurrentVersion;
            return state;
        }

        /// <summary>
        /// Fills in early combat defaults that older saves may be missing.
        /// </summary>
        private static GameState MigrateV1ToV2(GameState state)
        {
            if (state.MaxHandSize <= 0) state.MaxHandSize = 6;
            if (state.BitsPerTurn <= 0) state.BitsPerTurn = 25;
            if (state.Bits <= 0) state.Bits = 100;
            if (state.Phase == default) state.Phase = GamePhase.Betting;
            return state;
        }

        /// <summary>
        /// Adds nullable/reference-type fields introduced after the original save format.
        /// </summary>
        private static GameState MigrateV2ToV3(GameState state)
        {
            state.PendingChoice ??= null;
            state.GeneratedItems ??= new System.Collections.Generic.List<string>();
            state.LockedDeckCardIds ??= new System.Collections.Generic.List<string>();
            state.FullDeckCardIds ??= new System.Collections.Generic.List<string>();
            if (state.LastCheckpointMoney == 0) state.LastCheckpointMoney = Math.Max(0, state.Money);
            if (state.LastCheckpointPlayerHp == 0) state.LastCheckpointPlayerHp = state.Player.MaxHealth;
            return state;
        }

        /// <summary>
        /// Backfills job-loop fields introduced after between-combat work was added.
        /// </summary>
        private static GameState MigrateV3ToV4(GameState state)
        {
            if (state.JobFatigue < 0) state.JobFatigue = 0;
            if (state.JobsCompleted < 0) state.JobsCompleted = 0;
            state.LastJobType ??= null;
            return state;
        }

        /// <summary>
        /// Normalizes difficulty after scaling settings were introduced.
        /// </summary>
        private static GameState MigrateV4ToV5(GameState state)
        {
            state.DifficultyLevel = GameState.ClampDifficultyLevel(state.DifficultyLevel <= 0 ? GameState.DefaultDifficultyLevel : state.DifficultyLevel);
            return state;
        }

        /// <summary>
        /// Sanitizes the intermission packet fields added for cross-combat pacing effects.
        /// </summary>
        private static GameState MigrateV5ToV6(GameState state)
        {
            if (state.NextCombatBonusBits < 0) state.NextCombatBonusBits = 0;
            if (state.NextCombatEnemyAttackBonus < 0) state.NextCombatEnemyAttackBonus = 0;
            if (state.NextCombatBitsPenalty < 0) state.NextCombatBitsPenalty = 0;
            if (state.PendingGrowthRiskLossPenalty < 0) state.PendingGrowthRiskLossPenalty = 0;
            return state;
        }

        /// <summary>
        /// Backfills encounter + meta-progression data added in the latest progression pass.
        /// </summary>
        private static GameState MigrateV6ToV7(GameState state)
        {
            state.ActiveEncounterModifier = GameState.NormalizeEncounterModifier(state.ActiveEncounterModifier);
            if (state.MetaCredits < 0) state.MetaCredits = 0;
            if (state.LifetimeMetaCredits < 0) state.LifetimeMetaCredits = 0;
            if (state.CombatsWonThisRun < 0) state.CombatsWonThisRun = 0;
            if (state.RunsCompleted < 0) state.RunsCompleted = 0;
            if (state.RunsFailed < 0) state.RunsFailed = 0;
            state.UnlockTier = GameState.UnlockTierFromLifetimeMetaCredits(state.LifetimeMetaCredits);
            return state;
        }
    }
}
