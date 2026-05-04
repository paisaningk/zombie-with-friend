namespace Init.Demo
{
	/// <summary>
	/// Represents an object that holds settings data for the player character.
	/// </summary>
	public interface IMoveSettings
	{
		/// <summary>
		/// The speed at which the player character moves when move input is given.
		/// </summary>
		float MoveSpeed { get; }
	}
}