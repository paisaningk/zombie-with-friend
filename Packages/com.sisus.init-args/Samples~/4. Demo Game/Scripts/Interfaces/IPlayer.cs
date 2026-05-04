namespace Init.Demo
{
    /// <summary>
    /// Represents the player object.
    /// </summary>
    public interface IPlayer
    {
        /// <summary>
        /// Returns an <see cref="ITrackable"/> instance which can be used to track the position of the player object.
        /// </summary>
        ITrackable Trackable { get; }
    }
}
