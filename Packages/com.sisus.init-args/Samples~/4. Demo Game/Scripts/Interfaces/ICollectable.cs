namespace Init.Demo
{
    /// <summary>
    /// Represents an object which player object can collect by colliding with it.
    /// </summary>
    public interface ICollectable
    {
        /// <summary>
        /// Collects the object.
        /// </summary>
        void Collect();
    }
}
