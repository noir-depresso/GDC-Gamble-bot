// Immutable startup configuration shared by the Discord bot layer.
public class BotConfig
{
    public string Token { get; }
    public string Prefix { get; } = "!";
    public string DatabasePath { get; }

    // The prefix is fixed for now; token and db path come from the environment.
    public BotConfig(string token, string databasePath)
    {
        Token = token;
        DatabasePath = databasePath;
    }
}
