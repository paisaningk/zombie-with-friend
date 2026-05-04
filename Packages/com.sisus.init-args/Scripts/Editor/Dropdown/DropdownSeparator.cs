namespace Sisus.Init.EditorOnly
{
	internal sealed class DropdownSeparator : DropdownItem
	{
		public static readonly DropdownSeparator Instance = new();

		internal override bool IsSeparator() => true;
		private DropdownSeparator() : base(new("SEPARATOR")) { }
	}
}