namespace Player
{
    /// <summary>
    /// Per-player lifecycle state. Server-authoritative, synced via <see cref="PlayerState"/>.
    /// Alive is 0 so it is the default value a freshly spawned player carries.
    /// </summary>
    public enum PlayerLifeState
    {
        Alive = 0,
        Downed = 1,
        Dead = 2,
    }
}
