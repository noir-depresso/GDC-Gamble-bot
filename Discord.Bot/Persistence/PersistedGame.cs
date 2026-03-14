using System;
using Game.Core.Models;

namespace DiscordBot.Persistence
{
    // Storage envelope for a whole saved game plus metadata used by the bot layer.
    public class PersistedGame
    {
        public Guid GameId { get; set; }
        public ulong ChannelId { get; set; }
        public ulong OwnerUserId { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
        public string LastInteractionId { get; set; } = string.Empty;
        public GameState State { get; set; } = new();
    }
}
