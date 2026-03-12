using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace DiscordBot.Application
{
    public class GameLockProvider
    {
        private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _locks = new();

        public async Task<IAsyncDisposable> AcquireAsync(ulong channelId, CancellationToken ct)
        {
            var gate = _locks.GetOrAdd(channelId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            return new Releaser(gate);
        }

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
