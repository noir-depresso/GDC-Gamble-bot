using System.Collections.Generic;
using Game.Core.Effects;

namespace Game.Core.Cards
{
    public class CardDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public int BaseCost { get; }
        public List<IEffect> Effects { get; }

        public CardDef(string id, string name, string description, int baseCost, List<IEffect> effects)
        {
            Id = id;
            Name = name;
            Description = description;
            BaseCost = baseCost;
            Effects = effects;
        }
    }
}