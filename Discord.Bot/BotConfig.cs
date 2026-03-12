public class BotConfig
{
    public string Token { get; }
    public string Prefix { get; } = "!";
    public string DatabasePath { get; }

    public BotConfig(string token, string databasePath)
    {
        Token = token;
        DatabasePath = databasePath;
    }
}
