using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Application
{
    // Serializes command execution per channel so concurrent Discord events cannot race game state.
    public class GameLockProvider
    {
        private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _locks = new();

        // Acquires the channel gate and returns a releaser for "await using" scopes.
        public async Task<IAsyncDisposable> AcquireAsync(ulong channelId, CancellationToken ct)
        {
            var gate = _locks.GetOrAdd(channelId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            return new Releaser(gate);
        }

        // Releases the semaphore when the command pipeline finishes.
        private sealed class Releaser : IAsyncDisposable
        {
            private readonly SemaphoreSlim _gate;

            public Releaser(SemaphoreSlim gate)
            {
                _gate = gate;
            }

            public ValueTask DisposeAsync()
            {
                _gate.Release();
                return ValueTask.CompletedTask;
            }
        }
    }
}
