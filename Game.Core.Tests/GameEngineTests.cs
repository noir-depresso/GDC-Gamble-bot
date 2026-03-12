using Game.Core.Cards;
using Game.Core.Engine;
using Game.Core.Models;
using Xunit;

namespace Game.Core.Tests;

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

        var next = engine.Apply(new StartNextCombatAction());

        Assert.True(next.Success);
        Assert.False(engine.State.IsOver);
        Assert.Equal(GamePhase.Betting, engine.State.Phase);
        Assert.Equal(1, engine.State.TurnNumber);
        Assert.Equal(6, engine.State.HandCardIds.Count);
    }

    [Fact]
    public void WorkJob_BetweenCombats_EarnsMoney_AndEnforcesCooldown()
    {
        var engine = new GameEngine(new PredictableRandom());
        engine.StartNewGame();

        engine.State.IsOver = true;
        engine.State.Phase = GamePhase.CombatEnded;
        engine.State.Money = -50;

        var job1 = engine.Apply(new WorkJobAction("delivery"));
        Assert.True(job1.Success);
        Assert.False(engine.State.InDebt);

        var job2 = engine.Apply(new WorkJobAction("delivery"));
        Assert.False(job2.Success);
        Assert.Contains("cooldown", job2.Errors[0]);
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

    private sealed record UnknownAction : GameAction;
}
