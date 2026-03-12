using System;
using Game.Core.Models;

namespace Game.Core.Migrations
{
    public class GameStateMigrator : IGameStateMigrator
    {
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
                    _ => state
                };
                version++;
            }

            state.GameStateVersion = GameState.CurrentVersion;
            return state;
        }

        private static GameState MigrateV1ToV2(GameState state)
        {
            if (state.MaxHandSize <= 0) state.MaxHandSize = 6;
            if (state.BitsPerTurn <= 0) state.BitsPerTurn = 25;
            if (state.Bits <= 0) state.Bits = 100;
            if (state.Phase == default) state.Phase = GamePhase.Betting;
            return state;
        }

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

        private static GameState MigrateV3ToV4(GameState state)
        {
            if (state.JobFatigue < 0) state.JobFatigue = 0;
            if (state.JobsCompleted < 0) state.JobsCompleted = 0;
            state.LastJobType ??= null;
            return state;
        }
    }
}
