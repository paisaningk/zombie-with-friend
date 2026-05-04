using System.Collections.Generic;

namespace Sisus.Init.EditorOnly.Internal
{
	/// <summary>
	/// Data source for <see cref="ClientsDropdownWindow"/>.
	/// </summary>
	internal sealed class ClientsDataSource : DropdownDataSource
	{
		private readonly HashSet<Clients> selectedItems;
		private readonly string title;
		private static TypeDropdownItem root;

		public ClientsDataSource(HashSet<Clients> selectedItems) : this("Availability", selectedItems) { }

		public ClientsDataSource(string title, HashSet<Clients> selectedItems)
		{
			this.title = title;
			this.selectedItems = selectedItems;
		}
		
		protected override DropdownItem GetData()
		{
			root = new(title);
			Add("In GameObject", Clients.InGameObject);
			Add("In Children", Clients.InChildren);
			Add("In Parents", Clients.InParents);
			Add("In Hierarchy Root Children", Clients.InHierarchyRootChildren);
			Add("In Scene", Clients.InScene);
			Add("In All Scenes", Clients.InAllScenes);
			Add("Everywhere", Clients.Everywhere);
			return root;

			void Add(string name, Clients value)
			{
				var isSelected = selectedItems.Contains(value);
				var item = new DropdownItem<ClientsDropdownWindow, Clients>(name, isSelected, value);
				root.AddChild(item);
				item.SetParent(root);
			}
		}
	}
}