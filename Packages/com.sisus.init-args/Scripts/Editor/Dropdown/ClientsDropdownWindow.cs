using UnityEditor.Callbacks;

namespace Sisus.Init.EditorOnly.Internal
{
	internal sealed class ClientsDropdownWindow : DropdownWindow<ClientsDropdownWindow, Clients>
	{
		[DidReloadScripts]
		private static void OnScriptReload() => CloseAllOpenWindows();
	}
}