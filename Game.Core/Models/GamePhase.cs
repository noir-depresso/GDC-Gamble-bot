namespace Game.Core.Models
{
    // Explicit phase model so command validation can rely on a clear turn state machine.
    public enum GamePhase
    {
        PreCombatPreview,
        Betting,
        PlayerMain,
        EnemyTurn,
        RoundEnd,
        CombatEnded
    }
}
