namespace Game
{
    /// <summary>
    /// Match-level state, synced by <see cref="GameManager"/>.
    ///
    /// Legal transitions (enforced per-site at each call, not by a central matrix — decision 0014):
    ///   Lobby    → Playing   WaveManager.TryStartMatch (host + everyone ready)
    ///   Playing  → Won        WaveManager.RunCampaign (final wave cleared)
    ///   Playing  → Lost       GameManager.CheckLose (no player Alive)
    ///   Won/Lost → Lobby      GameManager.RestartMatch (host Play Again — resets the match)
    /// Exit to MainMenu is a scene teardown (LobbyManager), not a transition within this enum.
    /// </summary>
    public enum GameState
    {
        Lobby = 0,
        Playing = 1,
        Won = 2,
        Lost = 3,
    }
}
