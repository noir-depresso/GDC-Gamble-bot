using System;

namespace DiscordBot.Application
{
    public static class CommandParser
    {
        public static bool TryParse(string raw, string prefix, out string command, out string[] args)
        {
            command = string.Empty;
            args = Array.Empty<string>();

            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (!raw.StartsWith(prefix, StringComparison.Ordinal)) return false;

            string content = raw.Substring(prefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(content)) return false;

            string[] parts = content.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            command = parts[0].ToLowerInvariant();
            args = parts.Length > 1 ? parts[1..] : Array.Empty<string>();
            return true;
        }
    }
}
