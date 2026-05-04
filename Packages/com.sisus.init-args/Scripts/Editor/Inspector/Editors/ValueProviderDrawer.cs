using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly
{
	/// <summary>
	/// Base class for custom editors for a scriptable object value providers that can be
	/// drawn inlined inside another editor with a prefix label - just like a property drawer.
	/// </summary>
	public abstract class ValueProviderDrawer : Editor
	{
		/// <summary>
		/// This can be overridden to return <see langword="true"/> to draw the default GUI
		/// for the value provider instead of using <see cref="Draw"/>.
		/// </summary>
		public virtual bool DrawDefaultGUI => false;
		
		public abstract void Draw([AllowNull] GUIContent label, [AllowNull] SerializedProperty anyProperty, [AllowNull] Type valueType);

		public sealed override void OnInspectorGUI() => Draw(null, null, null);

		/// <summary>
		/// This can be overridden to handle click events on the value provider's GUI.
		/// </summary>
		/// <param name="position"> Position of the clicked value provider GUI in the Inspector. </param>
		/// <param name="client"> Client that will receive the Init argument from the value provider. </param>
		/// <param name="valueType"> Type of the Init parameter whose value this provider returns. </param>
		/// <returns>
		/// <see langword="true"/> if the click was handled by this method; otherwise, <see langword="false"/>.
		/// </returns>
		/// <remarks>
		/// This method is only called automatically during click events when <see cref="DrawDefaultGUI"/>
		/// returns <see langword="true"/>.
		/// </remarks>
		public virtual bool OnClicked(Rect position, [AllowNull] Component client, [AllowNull] Type valueType) => false;
	}
}