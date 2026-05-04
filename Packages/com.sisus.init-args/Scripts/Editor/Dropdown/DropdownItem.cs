using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Sisus.Init.EditorOnly
{
	/// <summary>
	/// Base class for all dropdown items.
	/// </summary>
	internal class DropdownItem : IComparable
	{
		private static class Styles
		{
			public static readonly GUIStyle ItemStyle = new("PR Label") { alignment = TextAnchor.MiddleLeft, padding = new(0, 0, 2, 2), margin = new(0, 0, 0, 0) };
		}

		internal Vector2 scrollPosition;
		internal int selectedItem;

		protected GUIContent label;
		protected GUIContent labelWhenSearching;
		private DropdownItem parent;
		private readonly List<DropdownItem> children = new();

		public virtual GUIStyle lineStyle => Styles.ItemStyle;
		public GUIContent Content => label;
		public GUIContent ContentWhenSearching => labelWhenSearching;
		public string Name => label.text;
		public DropdownItem Parent => parent;
		public List<DropdownItem> Children => children;
		public bool HasChildren => Children.Any();
		public bool drawArrow => HasChildren;
		public bool searchable { get; set; }

		public int SelectedItem
		{
			get => selectedItem;

			set
			{
				if(value < 0)
				{
					selectedItem = 0;
				}
				else if(value >= Children.Count)
				{
					selectedItem = Children.Count - 1;
				}
				else
				{
					selectedItem = value;
				}
			}
		}

		public DropdownItem(GUIContent label) : this(label, label) { }

		public DropdownItem(GUIContent label, GUIContent labelWhenSearching)
		{
			this.label = label;
			this.labelWhenSearching = labelWhenSearching;
		}

		internal void AddChild(DropdownItem item) => Children.Add(item);
		internal void SetParent(DropdownItem item) => parent = item;
		internal void AddSeparator() => Children.Add(DropdownSeparator.Instance);
		internal virtual bool IsSeparator() => false;
		public virtual bool OnAction() => true;

		public DropdownItem GetSelectedChild()
		{
			if(Children.Count == 0 || selectedItem < 0)
			{
				return null;
			}

			return Children[selectedItem];
		}

		public IEnumerable<DropdownItem> GetSearchableElements()
		{
			if(searchable)
			{
				yield return this;
			}

			foreach(var child in Children)
			{
				foreach(var searchableChildren in child.GetSearchableElements())
				{
					yield return searchableChildren;
				}
			}
		}

		public virtual int CompareTo(object obj) => string.Compare(Name, (obj as DropdownItem)?.Name, StringComparison.Ordinal);

		public void MoveDownSelection()
		{
			var selectedIndex = SelectedItem;
			do
			{
				++selectedIndex;
			}
			while(selectedIndex < Children.Count && Children[selectedIndex].IsSeparator());

			if(selectedIndex < Children.Count)
			{
				SelectedItem = selectedIndex;
			}
		}

		public void MoveUpSelection()
		{
			var selectedIndex = SelectedItem;

			do
			{
				--selectedIndex;
			}
			while(selectedIndex >= 0 && Children[selectedIndex].IsSeparator());

			if(selectedIndex >= 0)
			{
				SelectedItem = selectedIndex;
			}
		}
	}
}