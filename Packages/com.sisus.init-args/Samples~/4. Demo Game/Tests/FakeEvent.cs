namespace Init.Demo.Tests
{
	public sealed class FakeEvent : IEventTrigger
	{
        public bool HasBeenTriggered { get; private set; }
		public void Trigger() => HasBeenTriggered = true;
	}
}