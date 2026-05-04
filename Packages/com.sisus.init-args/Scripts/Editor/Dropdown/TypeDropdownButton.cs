using System;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	internal sealed class TypeDropdownButton : DropdownButton
	{
		public TypeDropdownButton
		(
			GUIContent prefixLabel,
			GUIContent buttonLabel,
			DropdownDataSource dataSource,
			Action<Type> onTypeSelected
		)
			: base(prefixLabel, buttonLabel, belowRect => TypeDropdownWindow.Show(belowRect, dataSource, onTypeSelected)) { }
	}
}