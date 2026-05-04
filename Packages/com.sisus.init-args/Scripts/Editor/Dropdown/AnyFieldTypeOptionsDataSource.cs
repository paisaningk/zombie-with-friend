using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sisus.Init.Internal;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sisus.Init.EditorOnly.Internal
{
	internal sealed class AnyFieldTypeOptionsDataSource : DropdownDataSource
	{
		private readonly SerializedProperty valueProperty;
		private readonly Type valueType;
		private readonly bool isService;
		private readonly HashSet<Type> selectedItems;
		private readonly string title;
		private readonly bool canBeUnityObject;
		private readonly bool drawNullOption;

		private TypeDropdownItem root;

		private bool? hasMultipleOptions;
		private bool? hasMultipleNonValueProviderOptions;
		public bool HasMultipleOptions
		{
			get
			{
				if(hasMultipleOptions is null)
				{
					UpdateHasMultipleOptionsResults();
				}

				return hasMultipleOptions!.Value;
			}
		}

		public bool HasMultipleNonValueProviderOptions
		{
			get
			{
				if(hasMultipleNonValueProviderOptions is null)
				{
					UpdateHasMultipleOptionsResults();
				}

				return hasMultipleNonValueProviderOptions!.Value;
			}
		}

		private void UpdateHasMultipleOptionsResults()
		{
			if(valueType is null || valueProperty is null)
			{
				hasMultipleOptions = hasMultipleNonValueProviderOptions = false;
				return;
			}

			var hasItem = isService | canBeUnityObject | drawNullOption;

			if(!valueType.IsValueType)
			{
				if(!valueType.IsAbstract
				   && !valueType.IsGenericTypeDefinition
				   && !typeof(Object).IsAssignableFrom(valueType)
				   // in theory a valid option, but in practice it's highly unlikely anybody
				   // will want to inject an instance of it, so I prefer to hide it in the menu,
				   // to reduce clutter and avoid potential confusion with UnityEngine.Object.
				   && valueType != typeof(object))
				{
					if(hasItem)
					{
						hasMultipleOptions = hasMultipleNonValueProviderOptions = true;
						return;
					}

					hasItem = true;
				}

				var typeOptions = TypeCache.GetTypesDerivedFrom(valueType)
					.Where(t => !t.IsAbstract
						&& !typeof(Object).IsAssignableFrom(t)
						&& !t.IsGenericTypeDefinition
						&& t.Name.IndexOf('=') is -1
						&& t.Name.IndexOf('<') is -1)
					.OrderBy(t => t.Name);

				using var typeOptionsEnumerator = typeOptions.GetEnumerator();
				if(typeOptionsEnumerator.MoveNext())
				{
					if(hasItem || typeOptionsEnumerator.MoveNext())
					{
						hasMultipleOptions = hasMultipleNonValueProviderOptions = true;
						return;
					}

					hasItem = true;
				}
			}

			hasMultipleNonValueProviderOptions = false;
			var valueProviderOptions = GetAllInitArgMenuItemValueProviderTypes(valueType);
			using var valueProviderOptionsEnumerator = valueProviderOptions.GetEnumerator();
			// If any value providers exist, then should always show the dropdown menu.
			// There should always be at the very least two choice in this case: the value provider, and either the concrete type (structs, most classes) or null (interfaces, abstract classes).
			hasMultipleOptions = valueProviderOptionsEnumerator.MoveNext();
		}

		public AnyFieldTypeOptionsDataSource(string title, SerializedProperty valueProperty, Type valueType, bool isService, bool canBeUnityObject, bool drawNullOption, HashSet<Type> selectedItems)
		{
			this.valueProperty = valueProperty;
			this.title = title;
			this.isService = isService;
			this.canBeUnityObject = canBeUnityObject;
			this.drawNullOption = drawNullOption;
			this.valueType = valueType;
			this.selectedItems = selectedItems;
		}

		protected override DropdownItem GetData()
		{
			if(root is not null)
			{
				return root;
			}

			hasMultipleOptions = false;
			root = new(title);

			if(valueType is null)
			{
				return root;
			}

			if(valueProperty is null)
			{
				AddToRoot(valueType);
				return root;
			}

			var addSeparator = false;
			if(isService)
			{
				AddToRoot("Service", null);
				addSeparator = true;
			}
			else if(canBeUnityObject)
			{
				AddToRoot("Reference", null);
				addSeparator = true;
			}
			else if(drawNullOption)
			{
				AddToRoot("None", null);
				addSeparator = true;
			}

			if(!valueType.IsAbstract
				&& !valueType.IsGenericTypeDefinition
				&& !typeof(Object).IsAssignableFrom(valueType)
				// in theory a valid option, but in practice it's highly unlikely anybody
				// will want to inject an instance of it, so I prefer to hide it in the menu,
				// to reduce clutter and avoid potential confusion with UnityEngine.Object.
				&& valueType != typeof(object))
			{
				if(addSeparator)
				{
					AddSeparatorToRoot();
				}

				AddToRoot(valueType);
				addSeparator = true;
			}

			IEnumerable<Type> typeOptions;

			// TypeCache.GetTypesDerivedFrom apparently doesn't include primitive types, even for typeof(object), typeof(IConvertible) etc.
			// Also, we want these to be at the top, where they are easier to find.
			if(valueType == typeof(object) || valueType.IsInterface)
			{
				if(valueType.IsAssignableFrom(typeof(bool)))
				{
					AddToRoot(typeof(bool));
				}

				if(valueType.IsAssignableFrom(typeof(int)))
				{
					AddToRoot(typeof(int));
				}

				if(valueType.IsAssignableFrom(typeof(float)))
				{
					AddToRoot(typeof(float));
				}

				if(valueType.IsAssignableFrom(typeof(double)))
				{
					AddToRoot(typeof(double));
				}

				if(valueType.IsAssignableFrom(typeof(string)))
				{
					AddToRoot(typeof(string));
				}

				typeOptions = TypeCache.GetTypesDerivedFrom(valueType)
					.Where(t => !t.IsAbstract
						&& !typeof(Object).IsAssignableFrom(t)
						&& !t.ContainsGenericParameters
						&& t != typeof(bool)
						&& t != typeof(int)
						&& t != typeof(float)
						&& t != typeof(double)
						&& t != typeof(string)
						&& t.Name.IndexOf('=') is -1
						&& t.Name.IndexOf('<') is -1)
					.OrderBy(t => t.Name);
			}
			else if(valueType.IsValueType || valueType == typeof(string))
			{
				typeOptions = Array.Empty<Type>();
			}
			else
			{
				typeOptions = TypeCache.GetTypesDerivedFrom(valueType)
						.Where(t => !t.IsAbstract
						            && !typeof(Object).IsAssignableFrom(t)
						            && !t.IsGenericTypeDefinition
						            && t.Name.IndexOf('=') is -1
						            && t.Name.IndexOf('<') is -1)
						.OrderBy(t => t.Name);
			}

			using var typeOptionsEnumerator = typeOptions.GetEnumerator();
			if(typeOptionsEnumerator.MoveNext())
			{
				if(addSeparator)
				{
					AddSeparatorToRoot();
				}

				do
				{
					AddToRoot(typeOptionsEnumerator.Current);
				}
				while(typeOptionsEnumerator.MoveNext());

				addSeparator = true;
			}

			var valueProviderOptions = GetAllInitArgMenuItemValueProviderTypes(valueType);
			using var valueProviderOptionsEnumerator = valueProviderOptions.GetEnumerator();

			if(!valueProviderOptionsEnumerator.MoveNext())
			{
				hasMultipleOptions = RootItemCount > 1;
				return root;
			}

			if(addSeparator)
			{
				AddSeparatorToRoot();
			}

			do
			{
				var item = valueProviderOptionsEnumerator.Current;
				AddValueProvider(item.itemName, item.type);
			}
			while(valueProviderOptionsEnumerator.MoveNext());

			hasMultipleOptions = RootItemCount > 1;
			return root;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddToRoot(Type type) => Add(root, TypeUtility.ToString(type), type);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void AddToRoot(string name, Type type) => Add(root, name, type);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void Add(DropdownItem group, string name, Type type)
		{
			var isSelected = selectedItems.Contains(type);
			var item = new TypeDropdownItem(new(name), name, isSelected, type) { searchable = true };
			group.AddChild(item);
			item.SetParent(group);
		}

		private void AddValueProvider(string path, Type type)
		{
			if (string.IsNullOrEmpty(path))
			{
				Add(root, path, type);
				return;
			}

			ReadOnlySpan<char> pathSpan = path.AsSpan();
			DropdownItem parent = root;

			var start = 0;
			int separatorIndex;

			while ((separatorIndex = pathSpan.Slice(start).IndexOf('/')) is not -1)
			{
				separatorIndex += start;
				ReadOnlySpan<char> nameSpan = pathSpan.Slice(start, separatorIndex - start);
				parent = GetOrAddGroup(parent, nameSpan);
				start = separatorIndex + 1;
			}
    
			// Add the final part
			ReadOnlySpan<char> finalNameSpan = pathSpan.Slice(start);
			string finalName = finalNameSpan.ToString();
			Add(parent, finalName, type);

			static DropdownItem GetOrAddGroup(DropdownItem parent, ReadOnlySpan<char> nameSpan)
			{
				foreach (var existing in parent.Children)
				{
					if (nameSpan.SequenceEqual(existing.Name.AsSpan()))
					{
						return existing;
					}
				}

				string name = nameSpan.ToString();
				var group = TypeDropdownItem.CreateGroup(name);
				group.SetParent(parent);
				parent.AddChild(group);
				return group;
			}
		}

		private void AddSeparatorToRoot()
		{
			var separator = DropdownSeparator.Instance;
			root.AddChild(separator);
			separator.SetParent(root);
		}
		
		private static IEnumerable<(Type type, ValueProviderMenuAttribute attribute, string itemName)> GetAllInitArgMenuItemValueProviderTypes(Type valueType)
		{
			return ValueProviderEditorUtility.GetAllValueProviderMenuItemTargetTypes()
												.Select(type =>
												{
													var attribute = type.GetCustomAttribute<ValueProviderMenuAttribute>();
													var itemName = !string.IsNullOrEmpty(attribute.ItemName) ? attribute.ItemName : TypeUtility.ToStringNicified(type);
													return (type, attribute, itemName);
												})
												.Where(x => x.attribute is { } attribute
												&& (attribute.IsAny.Length == 0 || Array.Exists(attribute.IsAny, t => t.IsAssignableFrom(valueType)))
												&& (attribute.NotAny.Length == 0 || !Array.Exists(attribute.NotAny, t => t.IsAssignableFrom(valueType)))
												&& MatchesAny(valueType, attribute.WhereAny)
												&& MatchesAll(valueType, attribute.WhereAll)
												&& (attribute.WhereNone == Is.Unconstrained || !MatchesAny(valueType, attribute.WhereNone)))
												.OrderBy(x => x.attribute.Order)
												.ThenBy(x => x.itemName);

			static bool MatchesAny(Type valueType, Is whereAny)
			{
				if(whereAny == Is.Unconstrained)
				{
					return true;
				}

				if((whereAny.HasFlag(Is.Class)			&& valueType.IsClass) ||
					(whereAny.HasFlag(Is.ValueType)		&& valueType.IsValueType) ||
					(whereAny.HasFlag(Is.Concrete)		&& !valueType.IsAbstract) ||
					(whereAny.HasFlag(Is.Abstract)		&& valueType.IsAbstract) ||
					(whereAny.HasFlag(Is.BuiltIn)		&& (valueType.IsPrimitive || valueType == typeof(string) || valueType == typeof(object))) ||
					(whereAny.HasFlag(Is.Interface)		&& valueType.IsInterface) ||
					(whereAny.HasFlag(Is.Component)		&& Find.typesToComponentTypes.ContainsKey(valueType)) ||
					(whereAny.HasFlag(Is.WrappedObject)	&& Find.typesToWrapperTypes.ContainsKey(valueType)) ||
					(whereAny.HasFlag(Is.SceneObject)	&& Find.typesToFindableTypes.ContainsKey(valueType) && (!typeof(Object).IsAssignableFrom(valueType) || typeof(Component).IsAssignableFrom(valueType) || valueType == typeof(GameObject))) ||
					(whereAny.HasFlag(Is.Asset)			&& Find.typesToFindableTypes.ContainsKey(valueType)) ||
					(whereAny.HasFlag(Is.Service)		&& ServiceUtility.IsServiceDefiningType(valueType)) ||
					(whereAny.HasFlag(Is.Collection)		&& TypeUtility.IsCommonCollectionType(valueType)))
				{
					return true;
				}

				return false;
			}

			static bool MatchesAll(Type valueType, Is whereAll)
			{
				if(whereAll == Is.Unconstrained)
				{
					return true;
				}

				if(whereAll.HasFlag(Is.Collection))
				{
					if(!typeof(IEnumerable).IsAssignableFrom(valueType))
					{
						return false;
					}

					var elementType = TypeUtility.GetCollectionElementType(valueType);
					if(elementType is null)
					{
						#if DEV_MODE && DEBUG_VALUE_PROVIDERS
						if(valueType != typeof(Transform))
						{
							Debug.Log($"Failed to get collection element type from {valueType}.");
						}
						#endif
						return false;
					}

					valueType = elementType;
				}

				if((!whereAll.HasFlag(Is.Class)			|| valueType.IsClass) &&
					(!whereAll.HasFlag(Is.ValueType)	|| valueType.IsValueType) &&
					(!whereAll.HasFlag(Is.Concrete)		|| !valueType.IsAbstract) &&
					(!whereAll.HasFlag(Is.Abstract)		|| valueType.IsAbstract) &&
					(!whereAll.HasFlag(Is.Interface)	|| valueType.IsInterface) &&
					(!whereAll.HasFlag(Is.Component)	|| Find.typesToComponentTypes.ContainsKey(valueType)) &&
					(!whereAll.HasFlag(Is.WrappedObject)|| Find.typesToWrapperTypes.ContainsKey(valueType)) &&
					(!whereAll.HasFlag(Is.SceneObject)	|| (Find.typesToFindableTypes.ContainsKey(valueType) && (!typeof(Object).IsAssignableFrom(valueType) || typeof(Component).IsAssignableFrom(valueType) || valueType == typeof(GameObject)))) &&
					(!whereAll.HasFlag(Is.Asset)		|| Find.typesToFindableTypes.ContainsKey(valueType)) &&
					(!whereAll.HasFlag(Is.Service)		|| ServiceUtility.IsServiceDefiningType(valueType)))
				{
					return true;
				}

				return false;
			}
		}
	}
}