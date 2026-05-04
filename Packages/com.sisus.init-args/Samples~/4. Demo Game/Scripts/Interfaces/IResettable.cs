namespace Init.Demo
{
    /// <summary>
    /// Represents objects that can be reset to their initial state.
    /// </summary>
    public interface IResettable
    {
        /// <summary>
        /// Resets the object to their initial state.
        /// </summary>
        void ResetState();
    }
}
