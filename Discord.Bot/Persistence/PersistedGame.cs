using System;
using Game.Core.Models;

namespace DiscordBot.Persistence
{
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
