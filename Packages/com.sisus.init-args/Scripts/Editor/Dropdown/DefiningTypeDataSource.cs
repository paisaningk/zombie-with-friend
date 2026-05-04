using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sisus.Init.Internal;
using UnityEditor;
using UnityEngine;

namespace Sisus.Init.EditorOnly.Internal
{
	/// <summary>
	/// Provides a list of all service defining type options for
	/// a particular target for display in a <see cref="DropdownWindow{TDropdownWindow,TValue}">dropdown</see>. 
	/// </summary>
	internal sealed class DefiningTypeDataSource : DropdownDataSource
	{
		private readonly object serviceOrServiceProvider;
		private readonly HashSet<Type> selectedItems;
		private readonly string title;

		private TypeDropdownItem root;
		private TypeDropdownItem valueProviderGroup;

		// Relies on the fact that all non-value provider options are listed in the root,
		// and all value provider options are listed in their own group.
		public int NonValueProviderOptions => GetData().Children.Count - (valueProviderGroup is null ? 0 : 1);

		public int ValueProviderOptions
		{
			get
			{
				GetData();
				return valueProviderGroup?.Children.Count ?? 0;
			}
		}

		public DefiningTypeDataSource(object serviceOrServiceProvider, HashSet<Type> selectedItems) : this("Defining Types", serviceOrServiceProvider, selectedItems) { }

		public DefiningTypeDataSource(string title, object serviceOrServiceProvider, HashSet<Type> selectedItems)
		{
			this.title = title;
			this.serviceOrServiceProvider = serviceOrServiceProvider;
			this.selectedItems = selectedItems;
		}
		
		protected override DropdownItem GetData()
		{
			if(root is not null)
			{
				return root;
			}

			root = new(title);

			if(serviceOrServiceProvider is null)
			{
				return root;
			}

			var serviceOrProviderType = serviceOrServiceProvider.GetType();
			AddToRoot(serviceOrProviderType);

			var addSeparator = true;

			var valueProviderValueTypesDetermined = false;
			if (serviceOrServiceProvider is IValueProvider or IValueProviderAsync)
			{
				foreach(var interfaceType in serviceOrProviderType.GetInterfaces())
				{
					if(!interfaceType.IsGenericType
						|| interfaceType.GetGenericTypeDefinition() is not { } typeDefinition
						|| (typeDefinition != typeof(IValueProvider<>) && typeDefinition != typeof(IValueProviderAsync<>)))
					{
						continue;
					}

					var valueType = interfaceType.GetGenericArguments()[0];
					if(TypeUtility.IsNullOrBaseType(valueType))
					{
						continue;
					}

					valueProviderValueTypesDetermined = true;

					AddSeparatorIfNeeded();
					AddProvidedValue(valueType);

					if(valueType.IsValueType)
					{
						continue;
					}

					for(var baseType = valueType.BaseType; !TypeUtility.IsNullOrBaseType(baseType); baseType = baseType.BaseType)
					{
						AddProvidedValue(baseType);
					}

					if(!valueType.IsInterface)
					{
						foreach(var derivedType in TypeCache.GetTypesDerivedFrom(valueType))
						{
							AddProvidedValue(derivedType);
						}

						foreach(var valueTypeInterface in valueType.GetInterfaces().Where(IncludeInterface))
						{
							AddProvidedValue(valueTypeInterface);
						}
					}
				}
			}

			if (!valueProviderValueTypesDetermined && serviceOrServiceProvider is IValueByTypeProvider valueByTypeProvider)
			{
				valueProviderValueTypesDetermined = true;
				AddSeparatorIfNeeded();
				foreach(var valueType in valueByTypeProvider.GetSupportedValueTypes())
				{
					AddProvidedValue(valueType);
				}
			}

			if (!valueProviderValueTypesDetermined && serviceOrServiceProvider is IValueByTypeProviderAsync valueByTypeProviderAsync)
			{
				AddSeparatorIfNeeded();
				foreach(var valueType in valueByTypeProviderAsync.GetSupportedValueTypes())
				{
					AddProvidedValue(valueType);
				}
			}

			for(var baseType = serviceOrProviderType.BaseType; !TypeUtility.IsNullOrBaseType(baseType); baseType = baseType.BaseType)
			{
				AddSeparatorIfNeeded();
				AddToRoot(baseType);
			}

			foreach(var serviceOrProviderInterface in serviceOrProviderType.GetInterfaces().Where(IncludeInterface))
			{
				AddSeparatorIfNeeded();
				AddToRoot(serviceOrProviderInterface);
			}

			return root;

			static bool IncludeInterface(Type type) => type.IsGenericType ? !ignoredGenericInterfaces.Contains(type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition()) : !ignoredNonGenericInterfaces.Contains(type);

			void AddToRoot(Type type)
			{
				var name = TypeUtility.ToString(type);
				var isSelected = selectedItems.Contains(type);
				var item = new TypeDropdownItem(new(name), name, isSelected, type) { searchable = true };
				root.AddChild(item);
				item.SetParent(root);
			}

			void AddProvidedValue(Type type)
			{
				var name = TypeUtility.ToString(type);
				var isSelected = selectedItems.Contains(type);
				var item = new TypeDropdownItem(new(name), name, isSelected, type) { searchable = true };
				GetOrCreateValueProviderGroup().AddChild(item);
				item.SetParent(valueProviderGroup);
			}

			void AddSeparatorIfNeeded()
			{
				if(addSeparator)
				{
					root.AddSeparator();
					addSeparator = false;
				}
			}
		}

		DropdownItem GetOrCreateValueProviderGroup()
		{
			if(valueProviderGroup is not null)
			{
				return valueProviderGroup;
			}

			valueProviderGroup = TypeDropdownItem.CreateGroup("Value Provider");
			valueProviderGroup.SetParent(root);
			root.AddChild(valueProviderGroup);
			return valueProviderGroup;
		}

		private static readonly HashSet<Type> ignoredNonGenericInterfaces = new()
		{
			typeof(ISerializationCallbackReceiver),
			typeof(IOneArgument),
			typeof(ITwoArguments),
			typeof(IThreeArguments),
			typeof(IFourArguments),
			typeof(IFiveArguments),
			typeof(ISixArguments),
			typeof(ISevenArguments),
			typeof(IEightArguments),
			typeof(INineArguments),
			typeof(ITenArguments),
			typeof(IElevenArguments),
			typeof(ITwelveArguments),
			typeof(IWrapper),
			typeof(IInitializable),
			typeof(IInitializableEditorOnly),
			typeof(IEnableable),
			typeof(IComparable),
			typeof(IFormattable),
			typeof(IConvertible),
			typeof(IDisposable),
			typeof(IAsyncDisposable),
			typeof(IValueByTypeProvider),
			typeof(IValueByTypeProviderAsync),
			typeof(INullGuard),
			typeof(INullGuardByType),
			typeof(IEnumerable),
			typeof(ICloneable),
			typeof(IValueProvider),
			typeof(IValueProviderAsync),
			typeof(IValueByTypeProvider),
			typeof(IValueByTypeProviderAsync),
			typeof(IAwake),
			typeof(IStart),
			typeof(IOnEnable),
			typeof(IUpdate),
			typeof(ILateUpdate),
			typeof(IFixedUpdate),
			typeof(IOnDestroy)
		};
		
		private static readonly HashSet<Type> ignoredGenericInterfaces = new()
		{
			typeof(IEquatable<>),
			typeof(IComparable<>),
			typeof(IWrapper<>),
			typeof(IFirstArgument<>),
			typeof(ISecondArgument<>),
			typeof(IThirdArgument<>),
			typeof(IFourthArgument<>),
			typeof(IFifthArgument<>),
			typeof(ISixthArgument<>),
			typeof(ISeventhArgument<>),
			typeof(IEighthArgument<>),
			typeof(INinthArgument<>),
			typeof(ITenthArgument<>),
			typeof(IEleventhArgument<>),
			typeof(ITwelfthArgument<>),
			typeof(IArgs<>),
			typeof(IArgs<,>),
			typeof(IArgs<,,>),
			typeof(IArgs<,,,>),
			typeof(IArgs<,,,,>),
			typeof(IArgs<,,,,,>),
			typeof(IArgs<,,,,,,>),
			typeof(IArgs<,,,,,,,>),
			typeof(IArgs<,,,,,,,,>),
			typeof(IArgs<,,,,,,,,,>),
			typeof(IArgs<,,,,,,,,,,>),
			typeof(IArgs<,,,,,,,,,,,>),
			typeof(IInitializable<>),
			typeof(IInitializable<,>),
			typeof(IInitializable<,,>),
			typeof(IInitializable<,,,>),
			typeof(IInitializable<,,,,>),
			typeof(IInitializable<,,,,,>),
			typeof(IInitializable<,,,,,,>),
			typeof(IInitializable<,,,,,,,>),
			typeof(IInitializable<,,,,,,,,>),
			typeof(IInitializable<,,,,,,,,,>),
			typeof(IInitializable<,,,,,,,,,,>),
			typeof(IInitializable<,,,,,,,,,,,>),
			typeof(IValueProvider<>),
			typeof(IValueProviderAsync<>)
		};
	}
}