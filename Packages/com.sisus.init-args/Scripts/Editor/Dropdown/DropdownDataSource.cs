using System.Collections.Generic;
using UnityEngine;

namespace Sisus.Init.EditorOnly
{
	internal abstract class DropdownDataSource
	{
		private static readonly GUIContent searchLabel = new("Search");

		public DropdownItem mainTree { get; private set; }
		public DropdownItem searchTree { get; private set; }

		/// <summary>
		/// Can be used to calculate optimal dropdown window height.
		/// </summary>
		public int RootItemCount => mainTree?.Children.Count ?? 0;

		public void ReloadData() => mainTree = GetData();

		protected abstract DropdownItem GetData();

		public void RebuildSearch(string search) => searchTree = Search(search);

		protected DropdownItem Search(string searchString)
		{
			if(string.IsNullOrEmpty(searchString))
				return null;

			// Support multiple search words separated by spaces.
			var searchWords = searchString.ToLower().Split(' ');

			// We keep two lists. Matches that matches the start of an item always get first priority.
			var matchesStart = new List<DropdownItem>();
			var matchesWithin = new List<DropdownItem>();

			foreach(var e in mainTree.GetSearchableElements())
			{
				var name = e.Name.ToLower().Replace(" ", "");

				var didMatchAll = true;
				var didMatchStart = false;

				// See if we match ALL the search words.
				for(var w = 0; w < searchWords.Length; w++)
				{
					var search = searchWords[w];
					if(name.Contains(search))
					{
						// If the start of the item matches the first search word, make a note of that.
						if(w == 0 && name.StartsWith(search))
							didMatchStart = true;
					}
					else
					{
						// As soon as any word is not matched, we disregard this item.
						didMatchAll = false;
						break;
					}
				}

				// We always need to match all search words.
				// If we ALSO matched the start, this item gets priority.
				if(didMatchAll)
				{
					if(didMatchStart)
						matchesStart.Add(e);
					else
						matchesWithin.Add(e);
				}
			}

			matchesStart.Sort();
			matchesWithin.Sort();

			var searchTree = new DropdownItem(searchLabel);
			foreach(var element in matchesStart)
			{
				searchTree.AddChild(element);
			}

			foreach(var element in matchesWithin)
			{
				searchTree.AddChild(element);
			}

			return searchTree;
		}
	}
}