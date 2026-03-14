using System.Collections.Generic;

namespace DiscordBot.Application
{
    // Service-layer response envelope. Right now it only needs outbound messages.
    public class GameServiceResult
    {
        public List<string> Messages { get; } = new();
    }
}
