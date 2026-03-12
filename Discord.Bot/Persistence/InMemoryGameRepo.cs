using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Persistence
{
    public class InMemoryGameRepo : IGameRepo
    {
        private readonly ConcurrentDictionary<ulong, PersistedGame> _gamesByChannel = new();

        public Task<PersistedGame?> LoadByChannelAsync(ulong channelId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _gamesByChannel.TryGetValue(channelId, out var game);
            return Task.FromResult(game);
        }

        public Task SaveAsync(PersistedGame game, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _gamesByChannel[game.ChannelId] = game;
            return Task.CompletedTask;
        }

        public Task DeleteByChannelAsync(ulong channelId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            _gamesByChannel.TryRemove(channelId, out _);
            return Task.CompletedTask;
        }
    }
}
