using System;
using UnityEditor.Callbacks;

namespace Sisus.Init.EditorOnly.Internal
{
	internal sealed class TypeDropdownWindow : DropdownWindow<TypeDropdownWindow, Type>
	{
		[DidReloadScripts]
		private static void OnScriptReload() => CloseAllOpenWindows();
	}
}