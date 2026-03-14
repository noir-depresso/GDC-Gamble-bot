using Game.Core.Cards;
using Game.Core.Engine;
using Game.Core.Models;
using Xunit;
using Game.Core.Random;

namespace Game.Core.Tests;

/// <summary>
/// High-value regression tests for the current prototype rules.
/// The intent is to lock down the player-facing behaviors that are most likely to break during iteration.
/// </summary>
public class GameEngineTests
{
    [Fact]
    public void CreateInitialState_HasExpectedStartingShape()
    {
        var engine = new GameEngine(new PredictableRandom());
        var state = engine.CreateInitialState(CharacterClass.Thief);

        Assert.Equal(1, state.TurnNumber);
        Assert.False(state.IsOver);
        Assert.Equal(6, state.HandCardIds.Count);
        Assert.Equal(CharacterClass.Thief, state.CharacterClass);
        Assert.Equal(32, state.FullDeckCardIds.Count);
        Assert.Equal(3, state.LockedDeckCardIds.Count);
    }

    [Fact]
    public void PlaceBet_ConsumesMoney_AndAddsBits()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        int moneyBefore = engine.State.Money;
        int bitsBefore = engine.State.Bits;

        var update = engine.Apply(new PlaceBetAction(50));

        Assert.True(update.Success);
        Assert.Equal(moneyBefore - 50, engine.State.Money);
        Assert.Equal(bitsBefore + 50, engine.State.Bits);
        Assert.Equal(50, engine.State.BetAmount);
    }

    [Fact]
    public void BuyLow_ReducesFinalCostBy20PercentPerStack()
    {
        var engine = new GameEngine(new PredictableRandom());
        var state = engine.CreateInitialState(CharacterClass.Thief);

        var card = CardLibrary.GetById("TROJAN");

        Assert.Equal(100, engine.FinalCost(state, card));

        state.AddStacks("BUY_LOW", 1, -1);
        Assert.Equal(80, engine.FinalCost(state, card));

        state.AddStacks("BUY_LOW", 1, -1);
        Assert.Equal(60, engine.FinalCost(state, card));
    }

    [Fact]
    public void Persuasion_CreatesPendingChoice_AndChoiceResolves()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.HandCardIds.Clear();
        engine.State.HandCardIds.Add("PERSUASION");
        engine.State.Bits = 999;

        var play = engine.Apply(new PlayCardAction(0));
        Assert.True(play.Success);
        Assert.NotNull(engine.State.PendingChoice);

        string choiceId = engine.State.PendingChoice!.ChoiceId;
        var choose = engine.Apply(new ChooseOptionAction(choiceId, "enemy_debuff"));

        Assert.True(choose.Success);
        Assert.Null(engine.State.PendingChoice);
    }

    [Fact]
    public void Expose_Choice_SacrificesCard_AndDeals75()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.HandCardIds.Clear();
        engine.State.HandCardIds.Add("EXPOSE");
        engine.State.HandCardIds.Add("THERAPY");
        engine.State.Bits = 999;

        int hpBefore = engine.State.Enemy.CurrentHealth;
        var play = engine.Apply(new PlayCardAction(0));
        Assert.True(play.Success);
        Assert.NotNull(engine.State.PendingChoice);

        string choiceId = engine.State.PendingChoice!.ChoiceId;
        var choose = engine.Apply(new ChooseOptionAction(choiceId, "0"));

        Assert.True(choose.Success);
        Assert.Equal(hpBefore - 75, engine.State.Enemy.CurrentHealth);
    }

    [Fact]
    public void EndTurn_AdvancesTurn_AndRespectsMaxHand()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        var update = engine.Apply(new EndTurnAction());

        Assert.True(update.Success);
        Assert.Equal(2, engine.State.TurnNumber);
        Assert.Equal(6, engine.State.HandCardIds.Count);
        Assert.True(update.StateChanged);
    }

    [Fact]
    public void StartNextCombat_AfterWin_ResetsCombatState()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.Enemy.CurrentHealth = 20;
        engine.State.HandCardIds.Clear();
        engine.State.HandCardIds.Add("TROJAN");
        engine.State.Bits = 999;

        var play = engine.Apply(new PlayCardAction(0));
        Assert.True(play.Success);
        Assert.True(engine.State.IsOver);
        Assert.Equal(GamePhase.CombatEnded, engine.State.Phase);

        string choiceId = engine.State.PendingChoice!.ChoiceId;
        var choose = engine.Apply(new ChooseOptionAction(choiceId, "safe"));
        Assert.True(choose.Success);

        var next = engine.Apply(new StartNextCombatAction());

        Assert.True(next.Success);
        Assert.False(engine.State.IsOver);
        Assert.Equal(GamePhase.Betting, engine.State.Phase);
        Assert.Equal(1, engine.State.TurnNumber);
        Assert.Equal(6, engine.State.HandCardIds.Count);
    }

    [Fact]
    public void WorkJob_BetweenCombats_EarnsMoney_AndAppliesRepeatPenalty()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.IsOver = true;
        engine.State.Phase = GamePhase.CombatEnded;
        engine.State.Money = -50;

        var job1 = engine.Apply(new WorkJobAction("delivery"));
        Assert.True(job1.Success);
        Assert.False(engine.State.InDebt);

        int moneyAfterFirstJob = engine.State.Money;

        var job2 = engine.Apply(new WorkJobAction("delivery"));
        Assert.True(job2.Success);
        Assert.True(engine.State.Money > moneyAfterFirstJob);
        Assert.Contains("Repeated job penalty", string.Join("\n", job2.Messages));
    }

    [Fact]
    public void StartNextCombat_WithDebt_ResetsToCheckpoint()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.IsOver = true;
        engine.State.Phase = GamePhase.CombatEnded;
        engine.State.Money = -120;
        engine.State.LastCheckpointMoney = 300;
        engine.State.Player.CurrentHealth = 10;
        engine.State.LastCheckpointPlayerHp = 70;

        var next = engine.Apply(new StartNextCombatAction());

        Assert.True(next.Success);
        Assert.Equal(300, engine.State.Money);
        Assert.Equal(70, engine.State.Player.CurrentHealth);
        Assert.False(engine.State.InDebt);
        Assert.False(engine.State.IsOver);
        Assert.Equal(GamePhase.Betting, engine.State.Phase);
    }

    [Fact]
    public void Apply_UnknownAction_ReturnsUnsupportedError()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        var update = engine.Apply(new UnknownAction());

        Assert.False(update.Success);
        Assert.Contains("Unsupported action type", update.Errors[0]);
    }

    [Fact]
    public void DifficultyScalesIncomingDamage()
    {
        var state = new GameState();

        state.DifficultyLevel = 1;
        int easyDamage = state.ScaleIncomingDamage(20);

        state.DifficultyLevel = 5;
        int hardDamage = state.ScaleIncomingDamage(20);

        Assert.True(hardDamage > easyDamage);
    }

    [Fact]
    public void EndTurn_DifficultyScalesRoundMoneyGain()
    {
        var easy = new GameEngine(new PredictableRandom());
        easy.StartNewGame();
        easy.State.DifficultyLevel = 1;
        int easyMoneyBefore = easy.State.Money;
        easy.Apply(new EndTurnAction());
        int easyMoneyGain = easy.State.Money - easyMoneyBefore;

        var hard = new GameEngine(new PredictableRandom());
        hard.StartNewGame();
        hard.State.DifficultyLevel = 5;
        int hardMoneyBefore = hard.State.Money;
        hard.Apply(new EndTurnAction());
        int hardMoneyGain = hard.State.Money - hardMoneyBefore;

        Assert.True(easyMoneyGain > hardMoneyGain);
    }

    [Fact]
    public void WinningCombat_CreatesIntermissionPacketChoice()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.Enemy.CurrentHealth = 20;
        engine.State.HandCardIds.Clear();
        engine.State.HandCardIds.Add("TROJAN");
        engine.State.Bits = 999;

        var play = engine.Apply(new PlayCardAction(0));

        Assert.True(play.Success);
        Assert.NotNull(engine.State.PendingChoice);
        Assert.Equal("INTERMISSION_PACKET", engine.State.PendingChoice!.ChoiceType);
        Assert.Contains("convert", engine.State.PendingChoice.Options);
    }

    [Fact]
    public void StartNextCombat_AutoResolvesIntermissionPacketAsBank()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.Enemy.CurrentHealth = 20;
        engine.State.HandCardIds.Clear();
        engine.State.HandCardIds.Add("TROJAN");
        engine.State.Bits = 999;
        engine.Apply(new PlayCardAction(0));

        var next = engine.Apply(new StartNextCombatAction());

        Assert.True(next.Success);
        Assert.Null(engine.State.PendingChoice);
        Assert.True(next.Messages.Exists(m => m != null && m.Contains("auto-resolved", System.StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void IntermissionCredit_GrantsPowerWithDrawbacks()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.Enemy.CurrentHealth = 20;
        engine.State.HandCardIds.Clear();
        engine.State.HandCardIds.Add("TROJAN");
        engine.State.Bits = 999;
        engine.Apply(new PlayCardAction(0));

        string choiceId = engine.State.PendingChoice!.ChoiceId;
        var choose = engine.Apply(new ChooseOptionAction(choiceId, "credit"));
        Assert.True(choose.Success);

        var next = engine.Apply(new StartNextCombatAction());

        Assert.True(next.Success);
        Assert.Equal(150, engine.State.Bits);
        Assert.Equal(26, engine.State.Enemy.Attack);
    }

    [Fact]
    public void Defeat_GrantsMetaCreditProgress()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.Player.CurrentHealth = 1;
        engine.State.Enemy.Attack = 999;

        var end = engine.Apply(new EndTurnAction());

        Assert.True(end.Success);
        Assert.True(engine.State.IsOver);
        Assert.Equal(1, engine.State.MetaCredits);
        Assert.Equal(1, engine.State.LifetimeMetaCredits);
        Assert.Equal(1, engine.State.RunsFailed);
    }

    [Fact]
    public void ThreeCombatWins_GrantsMilestoneMetaCredits()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        for (int i = 0; i < 3; i++)
        {
            engine.State.Enemy.CurrentHealth = 20;
            engine.State.HandCardIds.Clear();
            engine.State.HandCardIds.Add("TROJAN");
            engine.State.Bits = 999;

            var play = engine.Apply(new PlayCardAction(0));
            Assert.True(play.Success);

            if (i < 2)
            {
                var next = engine.Apply(new StartNextCombatAction());
                Assert.True(next.Success);
            }
        }

        Assert.Equal(3, engine.State.CombatsWonThisRun);
        Assert.Equal(14, engine.State.MetaCredits);
        Assert.Equal(14, engine.State.LifetimeMetaCredits);
        Assert.Equal(1, engine.State.UnlockTier);
    }

    [Fact]
    public void StartNextCombat_AnnouncesEncounterModifier()
    {
        var engine = new GameEngine(new MaxRandom());
        engine.StartNewGame();

        engine.State.IsOver = true;
        engine.State.Phase = GamePhase.CombatEnded;

        var next = engine.Apply(new StartNextCombatAction());

        Assert.True(next.Success);
        Assert.Equal(GameState.EncounterAudit, engine.State.ActiveEncounterModifier);
        Assert.Contains(next.Messages, m => m.Contains("Encounter modifier:", System.StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnlockTier_GrantsStartingBitsBonus()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.UnlockTier = 3;
        engine.State.IsOver = true;
        engine.State.Phase = GamePhase.CombatEnded;

        var next = engine.Apply(new StartNextCombatAction());

        Assert.True(next.Success);
        Assert.Equal(92, engine.State.Bits);
    }

    [Fact]
    public void HardDifficulty_RollsHarsherEncounterMoreOften()
    {
        var easy = new GameEngine(new EncounterRollRandom());
        easy.StartNewGame();
        easy.State.DifficultyLevel = 1;
        easy.State.IsOver = true;
        easy.State.Phase = GamePhase.CombatEnded;
        easy.Apply(new StartNextCombatAction());

        var hard = new GameEngine(new EncounterRollRandom());
        hard.StartNewGame();
        hard.State.DifficultyLevel = 5;
        hard.State.IsOver = true;
        hard.State.Phase = GamePhase.CombatEnded;
        hard.Apply(new StartNextCombatAction());

        Assert.Equal(GameState.EncounterNone, easy.State.ActiveEncounterModifier);
        Assert.Equal(GameState.EncounterMarketCrash, hard.State.ActiveEncounterModifier);
    }

    /// <summary>
    /// Returns the highest values so tests can force the harshest encounter branch.
    /// </summary>
    private sealed class MaxRandom : IRandom
    {
        public int Next(int minInclusive, int maxExclusive) => maxExclusive - 1;

        public double NextDouble() => 0.99;
    }

    /// <summary>
    /// Returns a specific encounter-roll boundary value so difficulty-weighted encounter logic can be compared.
    /// </summary>
    private sealed class EncounterRollRandom : IRandom
    {
        public int Next(int minInclusive, int maxExclusive)
        {
            if (maxExclusive == 100) return 20;
            return minInclusive;
        }

        public double NextDouble() => 0.5;
    }

    /// <summary>
    /// Minimal unknown action type used to assert the engine's unsupported-action guard path.
    /// </summary>
    private sealed record UnknownAction : GameAction;
}
