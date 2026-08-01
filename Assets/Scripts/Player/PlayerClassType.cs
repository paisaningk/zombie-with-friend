namespace Player
{
    /// <summary>
    /// The player's chosen class (decision 0012). Selected at the lobby (task 14); for now every
    /// player defaults to <see cref="Support"/> so the Heal Pulse ability is exercisable.
    ///
    /// Gunner has no ability (high fire rate / medium damage); Support fires normally and gets the
    /// Heal Pulse. Class-specific STATS (maxHP, weapon, ability values) move into a ClassData SO in
    /// task 14 — this enum is only the identity tag that gates class behaviour.
    /// </summary>
    public enum PlayerClassType
    {
        Gunner = 0,
        Support = 1,
    }
}
