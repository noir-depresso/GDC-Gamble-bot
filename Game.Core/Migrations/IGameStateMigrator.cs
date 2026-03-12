using Game.Core.Models;

namespace Game.Core.Migrations
{
    public interface IGameStateMigrator
    {
        GameState Migrate(GameState state, int loadedVersion);
    }
}
