using Game.Core.Models;

namespace Game.Core.Migrations
{
    // Version migration contract for loading older serialized game states safely.
    public interface IGameStateMigrator
    {
        GameState Migrate(GameState state, int loadedVersion);
    }
}
