using System;
using System.Diagnostics.CodeAnalysis;
using Sisus.Init.EditorOnly.Internal;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly
{
	[CustomEditor(typeof(WaitForService))]
	internal sealed class WaitForServiceEditor : ValueProviderDrawer
	{
		public override bool DrawDefaultGUI => true;
		public override void Draw([AllowNull] GUIContent label, [AllowNull] SerializedProperty anyProperty, [AllowNull] Type valueType) { }
		public override bool OnClicked(Rect position, [AllowNull] Component client, [AllowNull] Type valueType)
			=> Event.current.button is 0
			&& EditorServiceTagUtility.PingServiceOfClient(client, valueType);
	}
}