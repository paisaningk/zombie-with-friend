using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	internal abstract class DropdownWindow<TDropdownWindow, TValue> : EditorWindow where TDropdownWindow : DropdownWindow<TDropdownWindow, TValue>
	{
		private static class Styles
		{
			public static GUIStyle background = "grey_border";
			public static GUIStyle previewHeader = new(EditorStyles.label);
			public static GUIStyle previewText = new(EditorStyles.wordWrappedLabel);

			static Styles()
			{
				previewText.padding.left += 3;
				previewText.padding.right += 3;
				previewHeader.padding.left += 3 - 2;
				previewHeader.padding.right += 3;
				previewHeader.padding.top += 3;
				previewHeader.padding.bottom += 2;
			}
		}

		internal static TDropdownWindow instance;
		
		private Action<TValue> onValueSelected;
		private DropdownDataSource dataSource;
		private AdvancedDropdownGUI gui = new();
		private DropdownItem currentlyRenderedTree;
		private string search = "";
		private DropdownItem animationTree;
		private float newAnimTarget;
		private long ticksLastFrame;
		private bool scrollToSelected = true;
		
		[NonSerialized]
		private bool dirtyList = true;

		public bool ShowHeader { get; set; } = true;

		private bool HasSearch => !string.IsNullOrEmpty(search);

		private void OnEnable()
		{
			dirtyList = true;
			instance = (TDropdownWindow)this;
			ShowHeader = true;
		}

		private void OnDisable() => instance = null;

		internal static void Show(Rect belowRect, DropdownDataSource dataSource, Action<TValue> onValueSelected)
		{
			CloseAllOpenWindows();

			if(Event.current != null && Event.current.type != EventType.Layout && Event.current.type != EventType.Repaint)
			{
				Event.current.Use();
			}

			if(belowRect.width < 200f)
			{
				belowRect.width = 200f;
			}

			instance = CreateAndInit(belowRect, dataSource, onValueSelected);
		}

		public void Select(TValue value)
		{
			onValueSelected?.Invoke(value);
			Close();
		}

		private static TDropdownWindow CreateAndInit(Rect belowRect, DropdownDataSource dataSource, Action<TValue> onValueSelected)
		{
			var window = CreateInstance<TDropdownWindow>();
			window.Init(belowRect, dataSource, onValueSelected);
			return window;
		}

		private void Init(Rect belowRect, DropdownDataSource dataSource, Action<TValue> onValueSelected)
		{
			belowRect = GUIUtility.GUIToScreenRect(belowRect);
			this.dataSource = dataSource;
			this.onValueSelected = onValueSelected;
			OnDirtyList();
			currentlyRenderedTree = HasSearch ? dataSource.searchTree : dataSource.mainTree;
			float minHeight = currentlyRenderedTree.Children.Count == 0 || currentlyRenderedTree.Children.Any(c => c.HasChildren)
				? AdvancedDropdownGUI.WindowHeight
				: 58f + currentlyRenderedTree.Children.Count * currentlyRenderedTree.Children[0].lineStyle.CalcHeight(GUIContent.none, 0f);
			float height = Mathf.Min(minHeight, AdvancedDropdownGUI.WindowHeight);
			ShowAsDropDown(belowRect, new(belowRect.width, height));
			Focus();
			wantsMouseMove = true;
		}

		internal void OnGUI()
		{
			GUI.Label(new(0f, 0f, EditorGUIUtility.currentViewWidth, position.height), GUIContent.none, Styles.background);

			if(dirtyList)
			{
				OnDirtyList();
			}

			HandleKeyboard();
			OnGUISearch();

			if(newAnimTarget != 0 && Event.current.type == EventType.Layout)
			{
				long now = DateTime.Now.Ticks;
				float deltaTime = (now - ticksLastFrame) / (float)TimeSpan.TicksPerSecond;
				ticksLastFrame = now;

				newAnimTarget = Mathf.MoveTowards(newAnimTarget, 0, deltaTime * 4);

				if(newAnimTarget == 0)
				{
					animationTree = null;
				}
				Repaint();
			}

			var anim = newAnimTarget;
			anim = Mathf.Floor(anim) + Mathf.SmoothStep(0, 1, Mathf.Repeat(anim, 1));

			if(anim == 0)
			{
				DrawDropdown(0, currentlyRenderedTree);
			}
			else if(anim < 0)
			{
				DrawDropdown(anim, currentlyRenderedTree);
				DrawDropdown(anim + 1, animationTree);
			}
			else
			{
				DrawDropdown(anim - 1, animationTree);
				DrawDropdown(anim, currentlyRenderedTree);
			}
		}

		private void OnDirtyList()
		{
			dirtyList = false;
			dataSource.ReloadData();

			if(HasSearch)
			{
				dataSource.RebuildSearch(search);
			}
		}

		private void OnGUISearch()
		{
			gui.DrawSearchField(false, search, (newSearch) =>
			{
				dataSource.RebuildSearch(newSearch);
				currentlyRenderedTree =
					string.IsNullOrEmpty(newSearch) ? dataSource.mainTree : dataSource.searchTree;
				search = newSearch;
			});
		}

		private void HandleKeyboard()
		{
			var e = Event.current;
			if(e.type != EventType.KeyDown)
			{
				return;
			}

			switch(e.keyCode)
			{
				case KeyCode.DownArrow:
					currentlyRenderedTree.MoveDownSelection();
					scrollToSelected = true;
					e.Use();
					return;
				case KeyCode.UpArrow:
					currentlyRenderedTree.MoveUpSelection();
					scrollToSelected = true;
					e.Use();
					return;
				case KeyCode.Return:
				case KeyCode.KeypadEnter:
					e.Use();

					if(currentlyRenderedTree.GetSelectedChild().Children.Any())
					{
						GoToChild(currentlyRenderedTree);
						return;
					}

					if(currentlyRenderedTree.GetSelectedChild().OnAction())
					{
						Close();
					}
					return;
			}

			if(HasSearch)
			{
				return;
			}

			switch(e.keyCode)
			{
				case KeyCode.LeftArrow:
				case KeyCode.Backspace:
					GoToParent();
					e.Use();
					return;
				case KeyCode.RightArrow:
					if(currentlyRenderedTree.GetSelectedChild().Children.Any())
					{
						GoToChild(currentlyRenderedTree);
					}
					e.Use();
					return;
				case KeyCode.Escape:
					Close();
					e.Use();
					return;
			}
		}

		private void DrawDropdown(float anim, DropdownItem group)
		{
			var areaPosition = position;
			var screenRect = gui.GetAnimRect(areaPosition, anim);
			GUILayout.BeginArea(screenRect);

			if(ShowHeader)
			{
				gui.DrawHeader(group, GoToParent);
			}

			DrawList(group);
			GUILayout.EndArea();
		}

		private void DrawList(DropdownItem item)
		{
			item.scrollPosition = GUILayout.BeginScrollView(item.scrollPosition);
			EditorGUIUtility.SetIconSize(gui.IconSize);
			Rect selectedRect = new Rect();
			for(var i = 0; i < item.Children.Count; i++)
			{
				var child = item.Children[i];
				bool selected = i == item.selectedItem;
				gui.DrawItem(child, selected, HasSearch);
				var rect = GUILayoutUtility.GetLastRect();
				if(selected)
				{
					selectedRect = rect;
				}

				if((Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDown) && !selected && rect.Contains(Event.current.mousePosition))
				{
					item.selectedItem = i;
					Event.current.Use();
				}

				if(Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
				{
					item.selectedItem = i;
					if(currentlyRenderedTree.GetSelectedChild().Children.Any())
					{
						GoToChild(currentlyRenderedTree);
					}
					else if(currentlyRenderedTree.GetSelectedChild().OnAction())
					{
						Close();
						LayoutUtility.ExitGUI();
					}

					Event.current.Use();
				}
			}

			EditorGUIUtility.SetIconSize(Vector2.zero);

			GUILayout.EndScrollView();

			if(scrollToSelected && Event.current.type == EventType.Repaint)
			{
				scrollToSelected = false;
				Rect scrollRect = GUILayoutUtility.GetLastRect();
				if(selectedRect.yMax - scrollRect.height > item.scrollPosition.y)
				{
					item.scrollPosition.y = selectedRect.yMax - scrollRect.height;
					Repaint();
				}

				if(selectedRect.y < item.scrollPosition.y)
				{
					item.scrollPosition.y = selectedRect.y;
					Repaint();
				}
			}
		}

		private void GoToParent()
		{
			if(currentlyRenderedTree.Parent == null)
			{
				return;
			}
				
			ticksLastFrame = DateTime.Now.Ticks;
			newAnimTarget = newAnimTarget > 0 ? newAnimTarget - 1 : -1;
			animationTree = currentlyRenderedTree;
			currentlyRenderedTree = currentlyRenderedTree.Parent;
		}

		private void GoToChild(DropdownItem parent)
		{
			ticksLastFrame = DateTime.Now.Ticks;
			newAnimTarget = newAnimTarget < 0 ? newAnimTarget + 1 : 1;
			currentlyRenderedTree = parent.GetSelectedChild();
			animationTree = parent;
		}

		protected static void CloseAllOpenWindows()
		{
			foreach(var window in Resources.FindObjectsOfTypeAll<TDropdownWindow>())
			{
				try
				{
					window.Close();
				}
				catch
				{
					try
					{
						DestroyImmediate(window);
					}
					#if DEV_MODE
					catch(Exception e)
					#else
					catch
					#endif
					{
						// ignored
						#if DEV_MODE
						Debug.Log(e);
						#endif
					}
				}
			}
		}
	}
}