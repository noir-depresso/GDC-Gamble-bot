using System;

namespace DiscordBot.Application
{
    // Keeps raw chat parsing separate from gameplay logic so command grammar can evolve independently.
    public static class CommandParser
    {
        // Parses a raw message into a normalized command name plus positional arguments.
        public static bool TryParse(string raw, string prefix, out string command, out string[] args)
        {
            command = string.Empty;
            args = Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (!raw.StartsWith(prefix, StringComparison.Ordinal)) return false;

            // The parser is intentionally simple: trim the prefix and split on spaces.
            string content = raw.Substring(prefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(content)) return false;

            string[] parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            command = parts[0].ToLowerInvariant();
            args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
            return true;
        }
    }
}
