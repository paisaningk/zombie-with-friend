using System;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	internal sealed class TypeDropdownItem : DropdownItem
	{
		private static class Styles
		{
			public static GUIStyle itemStyle = new("PR Label");
			public static Texture2D groupIcon;
			public static Texture2D selectedIcon;

			static Styles()
			{
				itemStyle.alignment = TextAnchor.MiddleLeft;
				itemStyle.padding.left = 0;
				itemStyle.fixedHeight = 20;
				itemStyle.margin = new(0, 0, 0, 0);

				groupIcon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
				selectedIcon = EditorGUIUtility.IconContent("Valid").image as Texture2D;
			}
		}

		private readonly string fullPath;
		private readonly Type value;

		public override GUIStyle lineStyle => Styles.itemStyle;

		public static TypeDropdownItem CreateGroup(string path) => new(path)
		{
			label = new(path, Styles.groupIcon)
		};

		public TypeDropdownItem(string menuTitle) : base(new(menuTitle)) { }

		public TypeDropdownItem(GUIContent label, string fullPath, bool selected, Type value) : base(label)
		{
			this.value = value;
			this.fullPath = fullPath;
			this.label = selected ? new(label.text, Styles.selectedIcon) : label;
			labelWhenSearching = new(label);
		}

		public override bool OnAction()
		{
			DropdownWindow<TypeDropdownWindow, Type>.instance.Select(value);
			return true;
		}

		public override string ToString()
		{
			return fullPath;
		}
	}
}