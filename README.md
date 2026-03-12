# GDC Gambling Bot

Discord card-combat prototype with a game engine in `Game.Core` and a Discord host in `Discord.Bot`.

## Prerequisites

- .NET SDK 10.0 (preview/nightly compatible with `net10.0`)
- A Discord bot application and bot token

## Environment Setup

Set your token before running:

```powershell
$env:DISCORD_TOKEN="your-token-here"
```

Temporary compatibility fallback is still enabled for `BOT_TOKEN`, but `DISCORD_TOKEN` is the standard going forward.

Optional DB path override:

```powershell
$env:GDC_DB_PATH="data/gdc-gambling-bot.db"
```

## Run the Bot

```powershell
dotnet run --project .\Discord.Bot\Discord.Bot.csproj
```

## Run Tests

No test project exists yet in this repository. Once tests are added, run:

```powershell
dotnet test
```

## Storage

SQLite is used for session + game-state persistence.

Default path:

`./data/gdc-gambling-bot.db`
