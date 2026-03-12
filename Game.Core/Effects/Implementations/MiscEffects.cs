using System;
using System.Collections.Generic;
using Game.Core.Models;

namespace Game.Core.Effects.Implementations
{
    public class NoOpEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;
        private readonly string _message;

        public NoOpEffect(string message)
        {
            _message = message;
        }

        public string Apply(EffectContext ctx) => _message;
    }

    public class PersuasionEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.PendingChoice = new PendingChoice
            {
                ChoiceId = Guid.NewGuid().ToString("N"),
                ChoiceType = "PERSUASION",
                SourceCardId = "PERSUASION",
                Prompt = "Choose: enemy_debuff or player_buff",
                Options = new List<string> { "enemy_debuff", "player_buff" }
            };
            return "Persuasion: pending choice created (`enemy_debuff` or `player_buff`).";
        }
    }

    public class DdosEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.PendingChoice = new PendingChoice
            {
                ChoiceId = Guid.NewGuid().ToString("N"),
                ChoiceType = "DDOS",
                SourceCardId = "DDOS",
                Prompt = "Choose: see_cards or see_probability",
                Options = new List<string> { "see_cards", "see_probability" }
            };
            return "D-dos: pending choice created (`see_cards` or `see_probability`).";
        }
    }

    public class ScapeGoatEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            ctx.State.PendingChoice = new PendingChoice
            {
                ChoiceId = Guid.NewGuid().ToString("N"),
                ChoiceType = "SCAPEGOAT",
                SourceCardId = "SCAPEGOAT",
                Prompt = "Choose blocked card type",
                Options = new List<string> { "Investment", "Medicate", "Bruiser", "Knight", "Special" }
            };
            return "ScapeGoat: choose a card type to block for 2 turns.";
        }
    }

    public class ExposeEffect : IEffect
    {
        public EffectTrigger Trigger => EffectTrigger.OnPlay;

        public string Apply(EffectContext ctx)
        {
            var options = new List<string>();
            for (int i = 0; i < ctx.State.HandCardIds.Count; i++)
                options.Add(i.ToString());

            if (options.Count == 0)
                return "Expose failed: no extra card in hand to sacrifice.";

            ctx.State.PendingChoice = new PendingChoice
            {
                ChoiceId = Guid.NewGuid().ToString("N"),
                ChoiceType = "EXPOSE",
                SourceCardId = "EXPOSE",
                Prompt = "Choose hand index to sacrifice for Expose",
                Options = options
            };
            return "Expose: choose one hand index to sacrifice; deal 75 on resolve.";
        }
    }
}
