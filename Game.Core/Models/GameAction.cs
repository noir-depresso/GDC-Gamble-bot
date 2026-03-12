namespace Game.Core.Models
{
    public abstract record GameAction;

    public sealed record PlaceBetAction(int Amount) : GameAction;
    public sealed record StartNextCombatAction : GameAction;
    public sealed record WorkJobAction(string JobType) : GameAction;
    public sealed record SelectCharacterAction(CharacterClass CharacterClass) : GameAction;
    public sealed record PlayCardAction(int HandIndex) : GameAction;
    public sealed record ChooseOptionAction(string ChoiceId, string OptionId) : GameAction;
    public sealed record UseGeneratedItemAction(int ItemIndex) : GameAction;
    public sealed record EndTurnAction : GameAction;
}
