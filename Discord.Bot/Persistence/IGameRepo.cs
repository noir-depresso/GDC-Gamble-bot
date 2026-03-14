using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Persistence
{
    // Persistence contract for loading, saving, and deleting a channel game snapshot.
    public interface IGameRepo
    {
        Task<PersistedGame?> LoadByChannelAsync(ulong channelId, CancellationToken ct);
        Task SaveAsync(PersistedGame game, CancellationToken ct);
        Task DeleteByChannelAsync(ulong channelId, CancellationToken ct);
    }
}
