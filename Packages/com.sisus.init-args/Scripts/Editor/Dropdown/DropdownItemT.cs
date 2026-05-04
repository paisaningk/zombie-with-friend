using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	internal sealed class DropdownItem<TDropdownWindow, TValue> : DropdownItem where TDropdownWindow : DropdownWindow<TDropdownWindow, TValue>
	{
		private static class Styles
		{
			public static readonly GUIStyle itemStyle = new("PR Label");
			public static readonly Texture2D groupIcon;
			public static readonly Texture2D selectedIcon;

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
		private readonly TValue value;

		public override GUIStyle lineStyle => Styles.itemStyle;

		public static DropdownItem<TDropdownWindow, TValue> CreateGroup(string path) => new(path)
		{
			label = new(path, Styles.groupIcon)
		};

		private DropdownItem(string menuTitle) : base(new(menuTitle))
		{
			searchable = false;
		}
		
		public DropdownItem(string label, bool selected, TValue value) : base(selected ? new(label, Styles.selectedIcon) : new(label))
		{
			this.value = value;
			fullPath = label;
			labelWhenSearching = new(this.label);
			searchable = true;
		}

		public override bool OnAction()
		{
			DropdownWindow<TDropdownWindow, TValue>.instance.Select(value);
			return true;
		}

		public override string ToString() => fullPath;
	}
}