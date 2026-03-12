using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Game.Core.Migrations;
using Game.Core.Models;
using Microsoft.Data.Sqlite;

namespace DiscordBot.Persistence
{
    public class SqliteGameRepo : IGameRepo
    {
        private readonly string _dbPath;
        private readonly string _connectionString;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly IGameStateMigrator _migrator;

        private volatile bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public SqliteGameRepo(string dbPath)
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={dbPath}";
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _migrator = new GameStateMigrator();
        }

        public async Task<PersistedGame?> LoadByChannelAsync(ulong channelId, CancellationToken ct)
        {
            await EnsureInitializedAsync(ct);

            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
SELECT s.GameId, s.ChannelId, s.OwnerUserId, s.CreatedAtUtc, s.UpdatedAtUtc, s.LastInteractionId, gs.Version, gs.StateJson
FROM Sessions s
JOIN GameStates gs ON gs.GameId = s.GameId
WHERE s.ChannelId = $channelId
LIMIT 1;";
            cmd.Parameters.AddWithValue("$channelId", (long)channelId);

            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct)) return null;

            string gameIdText = reader.GetString(0);
            ulong loadedChannelId = (ulong)reader.GetInt64(1);
            ulong ownerUserId = (ulong)reader.GetInt64(2);
            string createdText = reader.GetString(3);
            string updatedText = reader.GetString(4);
            string lastInteractionId = reader.IsDBNull(5) ? string.Empty : reader.GetString(5);
            int version = reader.GetInt32(6);
            string stateJson = reader.GetString(7);

            var state = JsonSerializer.Deserialize<GameState>(stateJson, _jsonOptions) ?? new GameState();
            state = _migrator.Migrate(state, version);

            return new PersistedGame
            {
                GameId = Guid.Parse(gameIdText),
                ChannelId = loadedChannelId,
                OwnerUserId = ownerUserId,
                CreatedAtUtc = DateTime.Parse(createdText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                UpdatedAtUtc = DateTime.Parse(updatedText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal),
                LastInteractionId = lastInteractionId,
                State = state
            };
        }

        public async Task SaveAsync(PersistedGame game, CancellationToken ct)
        {
            await EnsureInitializedAsync(ct);

            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var tx = (SqliteTransaction)await conn.BeginTransactionAsync(ct);

            string nowUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            string createdUtc = game.CreatedAtUtc == default ? nowUtc : game.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture);

            await using (var sessionsCmd = conn.CreateCommand())
            {
                sessionsCmd.Transaction = tx;
                sessionsCmd.CommandText = @"
INSERT INTO Sessions (GameId, ChannelId, OwnerUserId, CreatedAtUtc, UpdatedAtUtc, LastInteractionId)
VALUES ($gameId, $channelId, $ownerUserId, $createdAtUtc, $updatedAtUtc, $lastInteractionId)
ON CONFLICT(GameId) DO UPDATE SET
    ChannelId = excluded.ChannelId,
    OwnerUserId = excluded.OwnerUserId,
    UpdatedAtUtc = excluded.UpdatedAtUtc,
    LastInteractionId = excluded.LastInteractionId;";

                sessionsCmd.Parameters.AddWithValue("$gameId", game.GameId.ToString());
                sessionsCmd.Parameters.AddWithValue("$channelId", (long)game.ChannelId);
                sessionsCmd.Parameters.AddWithValue("$ownerUserId", (long)game.OwnerUserId);
                sessionsCmd.Parameters.AddWithValue("$createdAtUtc", createdUtc);
                sessionsCmd.Parameters.AddWithValue("$updatedAtUtc", nowUtc);
                sessionsCmd.Parameters.AddWithValue("$lastInteractionId", game.LastInteractionId ?? string.Empty);

                await sessionsCmd.ExecuteNonQueryAsync(ct);
            }

            string stateJson = JsonSerializer.Serialize(game.State, _jsonOptions);

            await using (var statesCmd = conn.CreateCommand())
            {
                statesCmd.Transaction = tx;
                statesCmd.CommandText = @"
INSERT INTO GameStates (GameId, Version, StateJson, UpdatedAtUtc)
VALUES ($gameId, $version, $stateJson, $updatedAtUtc)
ON CONFLICT(GameId) DO UPDATE SET
    Version = excluded.Version,
    StateJson = excluded.StateJson,
    UpdatedAtUtc = excluded.UpdatedAtUtc;";

                statesCmd.Parameters.AddWithValue("$gameId", game.GameId.ToString());
                statesCmd.Parameters.AddWithValue("$version", game.State.GameStateVersion);
                statesCmd.Parameters.AddWithValue("$stateJson", stateJson);
                statesCmd.Parameters.AddWithValue("$updatedAtUtc", nowUtc);

                await statesCmd.ExecuteNonQueryAsync(ct);
            }

            await tx.CommitAsync(ct);
        }

        public async Task DeleteByChannelAsync(ulong channelId, CancellationToken ct)
        {
            await EnsureInitializedAsync(ct);

            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(ct);

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Sessions WHERE ChannelId = $channelId;";
            cmd.Parameters.AddWithValue("$channelId", (long)channelId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_initialized) return;

            await _initLock.WaitAsync(ct);
            try
            {
                if (_initialized) return;

                string? dir = Path.GetDirectoryName(_dbPath);
                if (!string.IsNullOrWhiteSpace(dir))
                    Directory.CreateDirectory(dir);

                await using var conn = new SqliteConnection(_connectionString);
                await conn.OpenAsync(ct);

                await using (var pragmaCmd = conn.CreateCommand())
                {
                    pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
                    await pragmaCmd.ExecuteNonQueryAsync(ct);
                }

                await using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Sessions (
    GameId TEXT PRIMARY KEY,
    ChannelId INTEGER NOT NULL UNIQUE,
    OwnerUserId INTEGER NOT NULL,
    CreatedAtUtc TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    LastInteractionId TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS GameStates (
    GameId TEXT PRIMARY KEY,
    Version INTEGER NOT NULL,
    StateJson TEXT NOT NULL,
    UpdatedAtUtc TEXT NOT NULL,
    FOREIGN KEY(GameId) REFERENCES Sessions(GameId) ON DELETE CASCADE
);";
                    await cmd.ExecuteNonQueryAsync(ct);
                }

                await using (var alterCmd = conn.CreateCommand())
                {
                    alterCmd.CommandText = "ALTER TABLE Sessions ADD COLUMN LastInteractionId TEXT NOT NULL DEFAULT '';";
                    try { await alterCmd.ExecuteNonQueryAsync(ct); } catch { }
                }

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }
    }
}
