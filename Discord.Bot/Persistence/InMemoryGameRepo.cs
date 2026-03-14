using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Persistence
{
    // Fast ephemeral repository used for development and short-lived local sessions.
    public class InMemoryGameRepo : IGameRepo
    {
        private readonly ConcurrentDictionary<ulong, PersistedGame> _gamesByChannel = new();

        // Reads are simple dictionary lookups keyed by channel.
        public Task<PersistedGame?> LoadByChannelAsync(ulong channelId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _gamesByChannel.TryGetValue(channelId, out var game);
            return Task.FromResult(game);
        }

        // Saving overwrites the latest snapshot for the channel.
        public Task SaveAsync(PersistedGame game, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _gamesByChannel[game.ChannelId] = game;
            return Task.CompletedTask;
        }

        // Deleting removes any active snapshot for that channel.
        public Task DeleteByChannelAsync(ulong channelId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _gamesByChannel.TryRemove(channelId, out _);
            return Task.CompletedTask;
        }
    }
}
