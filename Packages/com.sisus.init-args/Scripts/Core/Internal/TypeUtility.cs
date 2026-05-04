using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;
using Component = UnityEngine.Component;
using Object = UnityEngine.Object;

namespace Sisus.Init.Internal
{
	/// <summary>
	/// Contains a collections of utility methods related to <see cref="Type"/>.
	/// </summary>
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static class TypeUtility
	{
		private static readonly HashSet<Type> baseTypes = new()
		{
			typeof(object),
			typeof(Object),
			typeof(Component),
			typeof(Behaviour),
			typeof(MonoBehaviour),
			typeof(ScriptableObject),
			typeof(StateMachineBehaviour),
			#if UNITY_EDITOR
			typeof(UnityEditor.EditorWindow),
			#endif
			#if UNITY_UGUI
			typeof(UnityEngine.EventSystems.UIBehaviour)
			#endif
		};

		private static readonly Dictionary<char, Dictionary<Type, string>> toStringCache = new(64)
		{
			{ '\0', new Dictionary<Type, string>(4096) {
				{ typeof(Serialization._Integer), "Integer" }, { typeof(int), "Integer" }, { typeof(uint), "UInteger" },
				{ typeof(Serialization._Float), "Float" }, { typeof(float), "Float" },
				{ typeof(Serialization._Double), "Double" }, { typeof(double), "Double" },
				{ typeof(Serialization._Boolean), "Boolean" }, { typeof(bool), "Boolean" },
				{ typeof(Serialization._String), "String" }, { typeof(string), "String" },
				{ typeof(short), "Short" }, { typeof(ushort), "UShort" },
				{ typeof(byte), "Byte" },{ typeof(sbyte), "SByte" },
				{ typeof(long), "Long" }, { typeof(ulong), "ULong" },
				{ typeof(object), "object" },
				{ typeof(decimal), "Decimal" }, { typeof(Serialization._Type), "Type" }
			} },
			{ '/', new Dictionary<Type, string>(4096) {
				{ typeof(Serialization._Integer), "Integer" }, { typeof(int), "Integer" }, { typeof(uint), "UInteger" },
				{ typeof(Serialization._Float), "Float" }, { typeof(float), "Float" },
				{ typeof(Serialization._Double), "Double" }, { typeof(double), "Double" },
				{ typeof(Serialization._Boolean), "Boolean" }, { typeof(bool), "Boolean" },
				{ typeof(Serialization._String), "String" }, { typeof(string), "String" },
				{ typeof(short), "Short" }, { typeof(ushort), "UShort" },
				{ typeof(byte), "Byte" },{ typeof(sbyte), "SByte" },
				{ typeof(long), "Long" }, { typeof(ulong), "ULong" },
				{ typeof(object), "System/Object" },
				{ typeof(decimal), "Decimal" }, { typeof(Serialization._Type), "System/Type" }
			} },
			{ '.', new Dictionary<Type, string>(4096) {
				{ typeof(Serialization._Integer), "Integer" }, { typeof(int), "Integer" }, { typeof(uint), "UInteger" },
				{ typeof(Serialization._Float), "Float" }, { typeof(float), "Float" },
				{ typeof(Serialization._Double), "Double" }, { typeof(double), "Double" },
				{ typeof(Serialization._Boolean), "Boolean" }, { typeof(bool), "Boolean" },
				{ typeof(Serialization._String), "String" }, { typeof(string), "String" },
				{ typeof(short), "Short" }, { typeof(ushort), "UShort" },
				{ typeof(byte), "Byte" },{ typeof(sbyte), "SByte" },
				{ typeof(long), "Long" }, { typeof(ulong), "ULong" },
				{ typeof(object), "System.Object" },
				{ typeof(decimal), "Decimal" }, { typeof(Serialization._Type), "System.Type" }
			} }
		};

		private static readonly Dictionary<Type, bool> isSerializableByUnityCache = new();

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		#if UNITY_EDITOR
		internal static UnityEditor.TypeCache.TypeCollection
		#else
		[return: NotNull]
		internal static IEnumerable<Type>
		#endif
			GetTypesWithAttribute<TAttribute>() where TAttribute : Attribute
		{
			#if UNITY_EDITOR
			return UnityEditor.TypeCache.GetTypesWithAttribute<TAttribute>();
			#else
			foreach(var type in GetAllTypesThreadSafe(typeof(TAttribute).Assembly))
			{
				if(type.IsDefined(typeof(TAttribute)))
				{
					yield return type;
				}
			}
			#endif
		}

		#if UNITY_EDITOR
		internal static UnityEditor.TypeCache.TypeCollection
		#else
		[return: NotNull]
		internal static IEnumerable<Type>
		#endif
		GetDerivedTypes([DisallowNull] Type inheritedType)
		{
			#if UNITY_EDITOR
			return UnityEditor.TypeCache.GetTypesDerivedFrom(inheritedType);
			#else
			foreach(var type in GetAllTypesThreadSafe(inheritedType.Assembly))
			{
				if(inheritedType.IsAssignableFrom(type))
				{
					yield return type;
				}
			}
			#endif
		}

		[return: NotNull]
		internal static IEnumerable<Type> GetOpenGenericTypeDerivedTypes([DisallowNull] Type openGenericType, bool concreteOnly)
		{
			if(openGenericType.IsSealed)
			{
				yield break;
			}

			if(openGenericType.IsInterface)
			{
				foreach(var assembly in GetAllAssembliesThreadSafe(openGenericType.Assembly))
				{
					foreach(var type in assembly.GetLoadableTypes(false))
					{
						if(type.IsAbstract && concreteOnly)
						{
							continue;
						}

						var interfaces = type.GetInterfaces();
						foreach(var interfaceType in interfaces)
						{
							if (interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == openGenericType)
							{
								yield return type;
							}
						}
					}
				}
			}

			foreach(var assembly in GetAllAssembliesThreadSafe(openGenericType.Assembly))
			{
				foreach(var type in assembly.GetLoadableTypes(false))
				{
					if(type.IsAbstract && concreteOnly)
					{
						continue;
					}

					for(var baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
					{
						if (baseType.IsGenericType && baseType.GetGenericTypeDefinition() == openGenericType)
						{
							yield return type;
						}
					}
				}
			}
		}

		[return: NotNull]
		internal static IEnumerable<Type> GetImplementingTypes<TInterface>()
		{
			#if DEV_MODE
			Debug.Assert(typeof(TInterface).IsInterface);
			#endif

			#if UNITY_EDITOR
			foreach(var type in UnityEditor.TypeCache.GetTypesDerivedFrom<TInterface>())
			{
			#else
			Type interfaceType = typeof(TInterface);
			foreach(var type in GetAllTypesThreadSafe(typeof(TInterface).Assembly))
			{
				if(!interfaceType.IsAssignableFrom(type))
				{
					continue;
				}
			#endif

				if(!type.IsInterface)
				{
					yield return type;
				}
			}
		}

		[return: NotNull]
		public static IEnumerable<Type> GetImplementingTypes([DisallowNull] Type interfaceType)
		{
			#if DEV_MODE
			Debug.Assert(interfaceType.IsInterface);
			#endif

			#if UNITY_EDITOR
			foreach(var type in UnityEditor.TypeCache.GetTypesDerivedFrom(interfaceType))
			{
			#else
			foreach(var type in GetAllTypesThreadSafe(interfaceType.Assembly))
			{
				if(!interfaceType.IsAssignableFrom(type))
				{
					continue;
				}
			#endif

				if(!type.IsInterface)
				{
					yield return type;
				}
			}
		}

		/// <summary>
		/// NOTE: Even if this looks unused, it can be used in builds.
		/// </summary>
		[return: NotNull]
		public static IEnumerable<Type> GetAllTypesThreadSafe([AllowNull] Assembly mustReferenceAssembly)
		{
			foreach(var assembly in GetAllAssembliesThreadSafe(mustReferenceAssembly))
			{
				foreach(var type in assembly.GetLoadableTypes(false))
				{
					yield return type;
				}
			}
		}

		[return: NotNull]
		public static IEnumerable<Type> GetAllTypesThreadSafe()
		{
			foreach(var assembly in GetAllAssembliesThreadSafe(null))
			{
				foreach(var type in assembly.GetLoadableTypes(false))
				{
					yield return type;
				}
			}
		}

		/// <summary>
		/// NOTE: Even if this looks unused, it can be used in builds.
		/// </summary>
		[return: NotNull]
		internal static IEnumerable<Type> GetAllTypesVisibleTo([DisallowNull] Type visibleTo)
		{
			var assemblyContainingType = visibleTo.Assembly;
			foreach(var type in assemblyContainingType.GetLoadableTypes(publicOnly: false))
			{
				if(!IsUnrelatedNestedFamilyType(type, visibleTo))
				{
					yield return type;
				}
			}

			foreach(var visibleAssembly in GetAllAssembliesVisibleToThreadSafe(visibleTo.Assembly))
			{
				if(visibleAssembly == assemblyContainingType)
				{
					continue;
				}

				foreach(var type in visibleAssembly.GetLoadableTypes(publicOnly: true))
				{
					yield return type;
				}
			}

			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			static bool IsUnrelatedNestedFamilyType(Type type, Type visibleTo)
			{
				// Nested private, protected, and private protected classes are only visible to their parent type, and types that share the same parent

				if(type is { IsNested : false } or { IsNestedPublic : true } or { IsNestedAssembly: true })
				{
					return false;
				}

				if(type is { IsNestedFamily : false })
				{
					return true;
				}

				var parentType = type.DeclaringType;
				return type.DeclaringType == visibleTo || parentType == visibleTo.DeclaringType;
			}
		}

		public static IEnumerable<Assembly> GetAllAssembliesVisibleToThreadSafe([DisallowNull] Assembly visibleTo)
		{
			var assembliesByName = AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic).ToDictionary(a => a.GetName().FullName, a => a);
			var directlyReferencedAssemblyNames = visibleTo.GetReferencedAssemblies();
			var referencedAssemblies = new HashSet<Assembly>(directlyReferencedAssemblyNames.Select(a => assembliesByName[a.FullName]));

			// Also add indirectly referenced assemblies
			foreach(var directlyReferencedAssemblyName in directlyReferencedAssemblyNames)
			{
				AddAssembliesReferencedBy(assembliesByName[directlyReferencedAssemblyName.FullName]);
			}

			return referencedAssemblies;

			void AddAssembliesReferencedBy(Assembly assembly)
			{
				foreach(var referencedAssemblyName in assembly.GetReferencedAssemblies())
				{
					if(assembliesByName.TryGetValue(referencedAssemblyName.FullName, out var referencedAssembly) && referencedAssemblies.Add(referencedAssembly))
					{
						AddAssembliesReferencedBy(referencedAssembly);
					}
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsValidGenericTypeArgument([DisallowNull] Type type) =>
			!type.IsByRefLike // ref struct
			&& !type.IsPointer
			&& type != typeof(void)
			&& type != typeof(TypedReference)
			&& type is not { IsAbstract: true, IsSealed: true }; // static class

		/// <summary>
		/// NOTE: Even if this looks unused, it can be used in builds.
		/// </summary>
		public static IEnumerable<Assembly> GetAllAssembliesThreadSafe([AllowNull] Assembly mustReferenceAssembly)
		{
			var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();

			string mustReferenceName = mustReferenceAssembly?.GetName().Name;

			for(int n = allAssemblies.Length - 1; n >= 0; n--)
			{
				var assembly = allAssemblies[n];

				// skip dynamic assemblies to prevent NotSupportedException from being thrown when calling GetTypes
				if(assembly.IsDynamic)
				{
					continue;
				}

				if(mustReferenceAssembly is null || assembly == mustReferenceAssembly)
				{
					yield return assembly;
					continue;
				}

				var referencedAssemblies = assembly.GetReferencedAssemblies();
				for(int r = referencedAssemblies.Length - 1; r >= 0; r--)
				{
					if(string.Equals(referencedAssemblies[r].Name, mustReferenceName))
					{
						yield return assembly;
						break;
					}
				}
			}
		}

		#if UNITY_EDITOR
		private static readonly Dictionary<Type, string> nicifiedTypeNameCache = new();
		#endif

		public static string ToStringNicified([DisallowNull] Type type)
		{
			var typeName = ToString(type);

			#if UNITY_EDITOR
			// Don't try to nicify generic types, it looks bad.
			if(type.IsGenericType)
			{
				return typeName;
			}

			var nicifiedName = UnityEditor.ObjectNames.NicifyVariableName(typeName);
			if(!type.IsInterface)
			{
				return nicifiedName;
			}

			if(nicifiedTypeNameCache.TryGetValue(type, out string cachedResult))
			{
				return cachedResult;
			}

			if(nicifiedName.StartsWith("I "))
			{
				nicifiedName = nicifiedName.Substring(2);
			}

			nicifiedTypeNameCache.Add(type, nicifiedName);
			return nicifiedName;
			#else
			return typeName;
			#endif
		}

		public static string ToString([AllowNull] Type type, char namespaceDelimiter = '\0') => type is null ? "Null" : ToString(type, namespaceDelimiter, toStringCache);
		internal static string ToString([AllowNull] IEnumerable<Type> types, string separator = ", ", char namespaceDelimiter = '\0') => string.Join(separator, types.Select(t => ToString(t, namespaceDelimiter)));
		internal static string ToString(Type[] types, string separator = ", ", char namespaceDelimiter = '\0') => string.Join(separator, types.Select(t => ToString(t, namespaceDelimiter)));
		internal static string ToString(Span<Type> types, string separator = ", ", char namespaceDelimiter = '\0') => string.Join(separator, types.ToArray().Select(t => ToString(t, namespaceDelimiter)));

		internal static string ToString([DisallowNull] Type type, char namespaceDelimiter, Dictionary<char, Dictionary<Type, string>> cache)
		{
			if(cache[namespaceDelimiter].TryGetValue(type, out string cached))
			{
				return cached;
			}

			var builder = new StringBuilder();
			ToString(type, builder, namespaceDelimiter, cache);
			string result = builder.ToString();
			cache[namespaceDelimiter][type] = result;
			return result;
		}

		internal static bool IsSerializableByUnity([DisallowNull] Type type)
		{
			if(!isSerializableByUnityCache.TryGetValue(type, out bool result))
			{
				result = type.IsSerializable || typeof(Object).IsAssignableFrom(type) || (type.Namespace is string namespaceName && namespaceName.Contains("Unity"));
				isSerializableByUnityCache.Add(type, result);
			}

			return result;
		}

		private static void ToString([DisallowNull] Type type, [DisallowNull] StringBuilder builder, char namespaceDelimiter, Dictionary<char, Dictionary<Type, string>> cache, Type[] genericTypeArguments = null)
		{
			// E.g. List<T> generic parameter is T, in which case we just want to append "T".
			if(type.IsGenericParameter)
			{
				builder.Append(type.Name);
				return;
			}

			if(type.IsArray)
			{
				builder.Append(ToString(type.GetElementType(), namespaceDelimiter, cache));
				int rank = type.GetArrayRank();
				switch(rank)
				{
					case 1:
						builder.Append("[]");
						break;
					case 2:
						builder.Append("[,]");
						break;
					case 3:
						builder.Append("[,,]");
						break;
					default:
						builder.Append('[');
						for(int n = 1; n < rank; n++)
						{
							builder.Append(',');
						}
						builder.Append(']');
						break;
				}
				return;
			}

			if(namespaceDelimiter != '\0' && type.Namespace != null)
			{
				var namespacePart = type.Namespace;
				if(namespaceDelimiter != '.')
				{
					namespacePart = namespacePart.Replace('.', namespaceDelimiter);
				}

				builder.Append(namespacePart);
				builder.Append(namespaceDelimiter);
			}

			// You can create instances of a constructed generic type.
			// E.g. Dictionary<int, string> instead of Dictionary<TKey, TValue>.
			if(type.IsConstructedGenericType)
			{
				genericTypeArguments = type.GenericTypeArguments;
			}

			// If this is a nested class type then append containing type(s) before continuing.
			var containingClassType = type.DeclaringType;
			if(containingClassType != null && type != containingClassType)
			{
				// GenericTypeArguments can't be fetched from the containing class type
				// so need to pass them along to the ToString method and use them instead of
				// the results of GetGenericArguments.
				ToString(containingClassType, builder, '\0', cache, genericTypeArguments);
				builder.Append('.');
			}

			if(!type.IsGenericType)
			{
				builder.Append(type.Name);
				return;
			}

			var nullableUnderlyingType = Nullable.GetUnderlyingType(type);
			if(nullableUnderlyingType != null)
			{
				// "Int?" instead of "Nullable<Int>"
				builder.Append(ToString(nullableUnderlyingType, '\0', cache));
				builder.Append("?");
				return;
			}

			var name = type.Name;

			// If type name doesn't end with `1, `2 etc. then it's not a generic class type
			// but type of a non-generic class nested inside a generic class.
			if(name[^2] == '`')
			{
				builder.Append(name.Substring(0, name.Length - 2));
				builder.Append('<');

				// E.g. TKey, TValue
				var genericTypes = type.GetGenericArguments();
				if(type.IsGenericTypeDefinition)
				{
					for(int i = genericTypes.Length - 1; i >= 1; i--)
					{
						builder.Append(", ");
					}
				}
				else
				{
					// Prefer using results of GenericTypeArguments over results of GetGenericArguments if available.
					int genericTypeArgumentsLength = genericTypeArguments.Length;
					if(genericTypeArgumentsLength > 0)
					{
						builder.Append(ToString(genericTypeArguments[0], '\0', cache));
					}
					else
					{
						builder.Append(ToString(genericTypes[0], '\0', cache));
					}

					for(int n = 1, count = genericTypes.Length; n < count; n++)
					{
						builder.Append(", ");

						if(genericTypeArgumentsLength > n)
						{
							builder.Append(ToString(genericTypeArguments[n], '\0', cache));
						}
						else
						{
							builder.Append(ToString(genericTypes[n], '\0', cache));
						}
					}
				}

				builder.Append('>');
			}
			else
			{
				builder.Append(name);
			}
		}

		public static bool IsNullOrBaseType([AllowNull, NotNullWhen(false), MaybeNullWhen(true)] Type type) => type is null || IsBaseType(type);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool IsBaseType([DisallowNull] Type type) => type.IsGenericType ? IsGenericBaseType(type) : baseTypes.Contains(type);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		// simply using namespace comparison instead of checking if the type is found in genericBaseTypes,
		// so that base types found inside optional add-on packages will also be detected properly
		// (e.g. SerializedMonoBehaviour<T...> and SerializedScriptableObject<T...>).
		public static bool IsGenericBaseType([DisallowNull] Type type) => type.IsGenericType && type.Namespace == typeof(MonoBehaviour<object>).Namespace;

		public static bool DerivesFromGenericBaseType([MaybeNull] Type type)
		{
			while((type = type.BaseType) != null)
			{
				if(IsGenericBaseType(type))
				{
					return true;
				}
			}

			return false;
		}

		public static bool TryGetGenericBaseType([DisallowNull] Type type, out Type baseType)
		{
			while((type = type.BaseType) != null)
			{
				if(IsGenericBaseType(type))
				{
					baseType = type;
					return true;
				}
			}

			baseType = null;
			return false;
		}

		public static TCollection ConvertToCollection<TCollection, TElement>(TElement[] source)
		{
			if(source is TCollection result)
			{
				return result;
			}

			if(typeof(TCollection).IsArray)
			{
				var elementType = typeof(TCollection).GetElementType();
				if(elementType == typeof(TElement))
				{
					return (TCollection)(object)source;
				}

				int count = source.Length;
				var array = Array.CreateInstance(elementType, count);
				Array.Copy(source, array, count);
				return (TCollection)(object)array;
			}

			if(typeof(TCollection).IsAbstract)
			{
				if(typeof(TCollection).IsGenericType)
				{
					var typeDefinition = typeof(TCollection).GetGenericTypeDefinition();
					if(typeDefinition == typeof(IEnumerable<>) || typeDefinition == typeof(IReadOnlyList<>) || typeDefinition == typeof(IList<>))
					{
						int count = source.Length;
						var elementType = GetCollectionElementType(typeof(TCollection));
						var array = Array.CreateInstance(elementType, count);
						Array.Copy(source, array, count);
						return (TCollection)(object)array;
					}
				}
			}

			try
			{
				return To<TCollection>.Convert(source);
			}
			catch
			{
				if(!typeof(IEnumerable).IsAssignableFrom(typeof(TCollection)))
				{
					throw new InvalidCastException($"Unable to convert from {ToString(typeof(TElement))}[] to {ToString(typeof(TCollection))}.\n{ToString(typeof(TCollection))} does not seem to be a collection type.");
				}

				throw new InvalidCastException($"Unable to convert from {ToString(typeof(TElement))}[] to {ToString(typeof(TCollection))}.\n{ToString(typeof(TCollection))} must have a public constructor with an IEnumerable<{typeof(TElement).Name}> parameter.");
			}
		}

		public static Type GetCollectionElementType(Type collectionType)
		{
			if(collectionType.IsArray)
			{
				return collectionType.GetElementType();
			}

			if(collectionType.IsGenericType)
			{
				Type typeDefinition = collectionType.GetGenericTypeDefinition();
				if(typeDefinition == typeof(List<>)
				|| typeDefinition == typeof(IEnumerable<>)
				|| typeDefinition == typeof(IList<>)
				|| typeDefinition == typeof(ICollection<>)
				|| typeDefinition == typeof(IReadOnlyCollection<>)
				|| typeDefinition == typeof(IReadOnlyList<>)
				|| typeDefinition == typeof(HashSet<>)
				|| typeDefinition == typeof(Queue<>)
				|| typeDefinition == typeof(Stack<>)
				|| typeDefinition == typeof(NativeArray<>)
				|| typeDefinition == typeof(ReadOnlyCollection<>)
				)
				{
					return collectionType.GetGenericArguments()[0];
				}

				if(typeDefinition == typeof(Dictionary<,>) || typeDefinition == typeof(IDictionary<,>))
				{
					return typeof(KeyValuePair<,>).MakeGenericType(collectionType.GetGenericArguments());
				}
			}
			else if(collectionType == typeof(IEnumerable) || collectionType == typeof(IList) || collectionType == typeof(ICollection))
			{
				return typeof(object);
			}

			foreach(var interfaceType in collectionType.GetInterfaces())
			{
				if(interfaceType.IsGenericType && interfaceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
				{
					return interfaceType.GetGenericArguments()[0];
				}
			}

			return null;
		}

		public static bool IsCommonCollectionType(Type collectionType)
		{
			if(collectionType.IsArray)
			{
				return true;
			}

			if(!collectionType.IsGenericType)
			{
				return collectionType == typeof(IEnumerable) || collectionType == typeof(IList) || collectionType == typeof(ICollection);
			}

			Type typeDefinition = collectionType.GetGenericTypeDefinition();

			return typeDefinition == typeof(List<>)
				|| typeDefinition == typeof(IEnumerable<>)
				|| typeDefinition == typeof(IList<>)
				|| typeDefinition == typeof(IReadOnlyList<>)
				|| typeDefinition == typeof(ICollection<>)
				|| typeDefinition == typeof(IReadOnlyCollection<>)
				|| typeDefinition == typeof(HashSet<>)
				|| typeDefinition == typeof(Queue<>)
				|| typeDefinition == typeof(Stack<>)
				|| typeDefinition == typeof(NativeArray<>)
				|| typeDefinition == typeof(ReadOnlyCollection<>)
				|| typeDefinition == typeof(Dictionary<,>)
				|| typeDefinition == typeof(IDictionary<,>);
		}
		
		[return: NotNull]
		internal static Type[] GetLoadableTypes([DisallowNull] this Assembly assembly, bool publicOnly)
		{
			try
			{
				return publicOnly ? assembly.GetExportedTypes() : assembly.GetTypes();
			}
			catch(NotSupportedException) //thrown if GetExportedTypes is called for a dynamic assembly
			{
				#if DEV_MODE
				Debug.LogWarning(assembly.GetName().Name + ".GetLoadableTypes() NotSupportedException\n" + assembly.FullName);
				#endif
				return Type.EmptyTypes;
			}
			catch(ReflectionTypeLoadException loadException)
			{
				#if DEV_MODE
				Debug.LogWarning(assembly.GetName().Name + ".GetLoadableTypes() " + loadException + "\n" + assembly.FullName);
				#endif

				return loadException.Types.Where(t => t is not null).ToArray();
			}
			#if DEV_MODE
			catch(Exception exception)
			{
				Debug.LogWarning(assembly.GetName().Name + ".GetLoadableTypes() " + exception + "\n" + assembly.FullName);
			#else
			catch(Exception)
			{
			#endif
				return Type.EmptyTypes;
			}
		}

		private static class To<TCollection>
		{
			private static readonly ConstructorInfo constructor;
			private static readonly object[] arguments = new object[1];
			
			static To()
			{
				var argumentType = GetCollectionElementType(typeof(TCollection));
				var parameterTypeGeneric = typeof(IEnumerable<>);
				var parameterType = parameterTypeGeneric.MakeGenericType(argumentType);
				var collectionType = !typeof(TCollection).IsAbstract ? typeof(TCollection): typeof(List<>).MakeGenericType(argumentType);
				constructor = collectionType.GetConstructor(new Type[] { parameterType });
			}

			public static TCollection Convert(object sourceArray)
			{
				arguments[0] = sourceArray;
				return (TCollection)constructor.Invoke(arguments);
			}
		}

		public static BaseTypeIterator GetBaseTypes([DisallowNull] this Type type) => new(type.BaseType);

		public struct BaseTypeIterator
		{
			private Type current;
			private bool isFirst;

			public BaseTypeIterator GetEnumerator() => this;

			internal BaseTypeIterator([AllowNull] Type type)
			{
				current = type;
				isFirst = true;
			}

			public bool MoveNext()
			{
				if(isFirst)
				{
					isFirst = false;
				}
				else
				{
					current = current.BaseType;
				}

				return !IsNullOrBaseType(current);
			}

			public Type Current => current;
		}
	}
}