using System.Collections.Generic;
using Game.Core.Effects;
using Game.Core.Models;

namespace Game.Core.Cards
{
    public class CardDef
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }
        public CardType Type { get; }
        public int BaseCostBits { get; }
        public List<IEffect> Effects { get; }

        public CardDef(string id, string name, CardType type, string description, int baseCostBits, List<IEffect> effects)
        {
            Id = id;
            Name = name;
            Type = type;
            Description = description;
            BaseCostBits = baseCostBits;
            Effects = effects;
        }
    }
}
