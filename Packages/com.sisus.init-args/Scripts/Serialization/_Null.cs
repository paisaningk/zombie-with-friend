using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sisus.Init.Internal;
using UnityEngine;
using static Sisus.NullExtensions;
using Object = UnityEngine.Object;

namespace Sisus.Init.ValueProviders
{
	/// <summary>
	/// Returns a <see langword="null"/> reference.
	/// <para>
	/// Can be used to specify that the value of a service type Init argument should be an Object reference, which hasn't been assigned yet.
	/// </para>
	/// </summary>
	internal sealed class _Null : ScriptableObject, IValueProvider<object>, IValueByTypeProvider
	#if UNITY_EDITOR
	, INullGuard
	#endif
	{
		/// <summary>
		/// Always returns <see langword="null"/>.
		/// </summary>
		public object Value => null;

		#if UNITY_EDITOR
		NullGuardResult INullGuard.EvaluateNullGuard([AllowNull] Component client) => NullGuardResult.Error("Value is null");
		#endif

		public bool TryGetFor<TValue>([AllowNull] Component client, out TValue value)
		{
			value = default;
			return true;
		}

		public bool TryGetFor(Component client, out object value)
		{
			value = default;
			return true;
		}

		bool IValueByTypeProvider.IsValueTypeSupported(Type valueType) => valueType.IsClass;
		IEnumerable<Type> IValueByTypeProvider.GetSupportedValueTypes() => TypeUtility.GetAllTypesThreadSafe().Where(t => t.IsClass);

		public static bool operator ==(_Null @null, object obj) => obj is _Null || obj == Null;
		public static bool operator !=(_Null @null, object obj) => !(obj is _Null) && obj != Null;
		public static bool operator ==(_Null @null, Object obj) => obj is _Null || !obj;
		public static bool operator !=(_Null @null, Object obj) => !(obj is _Null) && obj;
		public static bool operator ==(object obj, _Null @null) => obj is _Null || obj == Null;
		public static bool operator !=(object obj, _Null @null) => !(obj is _Null) && obj != Null;
		public static bool operator ==(Object obj, _Null @null) => obj is _Null || !obj;
		public static bool operator !=(Object obj, _Null @null) => !(obj is _Null) && obj;
		public static bool operator ==(_Null @null1, _Null @null2) => true;
		public static bool operator !=(_Null @null1, _Null @null2) => true;
		public override bool Equals(object other) => other is _Null || other == Null;
		public override int GetHashCode() => 0;
		public override string ToString() => "Null";
	}
}