public class BotConfig
{
    public string Token { get; }
    public string Prefix { get; } = "!";

    public BotConfig(string token) => Token = token;
}