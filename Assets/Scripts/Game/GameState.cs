namespace Game
{
    /// <summary>
    /// Match-level state, synced by <see cref="GameManager"/>. Playing/Won/Lost are used from
    /// task 8; Lobby lands with the full lifecycle + scene/UI transitions in task 15.
    /// </summary>
    public enum GameState
    {
        Lobby = 0,
        Playing = 1,
        Won = 2,
        Lost = 3,
    }
}
