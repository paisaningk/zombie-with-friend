using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Sisus.Init.Serialization;
using UnityEngine;
using static Sisus.Init.Internal.InitializerUtility;
using Component = UnityEngine.Component;
using Object = UnityEngine.Object;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sisus.Init.Internal
{
	/// <summary>
	/// Used by the <see cref="Services"/> component when registering plain old C# objects directly without a custom <see cref="Wrapper{T}"/>.
	/// </summary>
	[Init(HideInInspector = true, NullArgumentGuard = NullArgumentGuard.None)]
	internal sealed class ServiceWrapper : Wrapper, IWrapper<object>, IValueByTypeProvider, ISerializationCallbackReceiver, IInitializable<Type>
	{
		/// <summary>
		/// Concrete type of the plain old class object wrapped by this component.
		/// </summary>
		[SerializeField] _Type wrappedObjectType;
		[SerializeField] string wrappedObjectSerializedState;
		object wrapped;

		public Type WrappedObjectType => wrappedObjectType;
		public new object WrappedObject => wrapped;
		bool IWrapper.enabled => enabled;
		object IWrapper.WrappedObject => wrapped;

		[NotNull] MonoBehaviour IWrapper.AsMonoBehaviour => this;
		[NotNull] Object IWrapper.AsObject => this;

		bool IValueByTypeProvider.TryGetFor<TValue>(Component client, out TValue value)
		{
			if(wrapped is TValue result)
			{
				value = result;
				return true;
			}

			value = default;
			return false;
		}

		bool IValueByTypeProvider.IsValueTypeSupported(Type valueType) => valueType.IsAssignableFrom(wrappedObjectType);
		bool IValueByTypeProvider.HasValueFor<TValue>(Component client) => wrapped is TValue;

		public void Init(Type wrappedObjectType)
		{
			Debug.Assert(wrappedObjectType != null, wrappedObjectType);
			Debug.Assert(!wrappedObjectType.IsAbstract, wrappedObjectType);
			this.wrappedObjectType = wrappedObjectType;

			try
			{
				wrapped = Activator.CreateInstance(wrappedObjectType);
			}
			#if DEV_MODE
			catch(Exception e)
			{
				Debug.LogWarning($"Services on '{name}' failed to create instance of type {TypeUtility.ToString(wrappedObjectType)}: {e}", gameObject);
				wrapped = null;
			}
			#else
			catch
			{
				wrapped = null;
			}
			#endif

			hideFlags = HideFlags.HideInInspector;
			#if UNITY_EDITOR
			EditorUtility.SetDirty(this);
			#endif
		}

		private void Init(object wrapped)
		{
			this.wrapped = wrapped;
			Find.wrappedInstances[wrapped] = this;

			if(wrapped is ICoroutines { CoroutineRunner: null } coroutineUser)
			{
				coroutineUser.CoroutineRunner = this;
			}
		}

		#if UNITY_EDITOR
		void Reset()
		{
			if(InitArgs.TryGet(Context.Reset, this, out object wrapped))
			{
				this.wrapped = wrapped;
			}

			OnInitializableReset(this);
		}
		#endif

		void Awake()
		{
			if(wrapped is null)
			{
				return;
			}

			Find.wrappedInstances[wrapped] = this;

			if(wrapped is ICoroutines { CoroutineRunner: null } coroutineUser)
			{
				coroutineUser.CoroutineRunner = this;
			}

			if(wrapped is IAwake awake)
			{
				awake.Awake();
			}
		}

		void OnEnable()
		{
			if(wrapped is IUpdate update)
			{
				Updater.Subscribe(update);
			}

			if(wrapped is ILateUpdate lateUpdate)
			{
				Updater.Subscribe(lateUpdate);
			}

			if(wrapped is IFixedUpdate fixedUpdate)
			{
				Updater.Subscribe(fixedUpdate);
			}

			if(wrapped is IOnEnable onEnable)
			{
				onEnable.OnEnable();
			}

			if(wrapped is ICancellable cancellable)
			{
				cancellable.IsCancellationRequested = false;
			}
		}

		void Start()
		{
			if(wrapped is IStart start)
			{
				start.Start();
			}
		}

		void OnDisable()
		{
			if(wrapped is IUpdate update)
			{
				Updater.Unsubscribe(update);
			}

			if(wrapped is ILateUpdate lateUpdate)
			{
				Updater.Unsubscribe(lateUpdate);
			}

			if(wrapped is IFixedUpdate fixedUpdate)
			{
				Updater.Unsubscribe(fixedUpdate);
			}

			if(wrapped is IOnDisable onDisable)
			{
				onDisable.OnDisable();
			}

			if(wrapped is ICancellable cancellable)
			{
				cancellable.IsCancellationRequested = true;
			}
		}

		async void OnDestroy()
		{
			if(wrapped is null)
			{
				return;
			}

			Find.wrappedInstances.Remove(wrapped);

			if(wrapped is IOnDestroy onDestroy)
			{
				onDestroy.OnDestroy();
			}
			else if(wrapped is IDisposable disposable)
			{
				disposable.Dispose();
			}
			else if(wrapped is IAsyncDisposable asyncDisposable)
			{
				await asyncDisposable.DisposeAsync();
			}
		}

		void IInitializable<object>.Init(object wrapped) => Init(wrapped);

		private protected override object GetWrappedObject() => wrapped;

		public void OnBeforeSerialize()
		{
			if(wrapped?.GetType() is { IsSerializable: true } type)
			{
				wrappedObjectType = type;
				wrappedObjectSerializedState = JsonUtility.ToJson(wrapped);
			}
		}

		public void OnAfterDeserialize()
		{
			if(wrappedObjectType.Value is not { } type)
			{
				return;
			}

			if(string.IsNullOrEmpty(wrappedObjectSerializedState))
			{
				wrapped ??= Activator.CreateInstance(type);
				return;
			}

			try
			{
				wrapped = JsonUtility.FromJson(wrappedObjectSerializedState, type);
			}
			catch(Exception e)
			{
				Debug.LogWarning($"Services component on '{name}' failed to deserialize wrapped object of type {wrappedObjectType}: {e}", gameObject);
				wrappedObjectSerializedState = "";
			}
		}

		/// <summary>
		/// Gets all value types that this value provider supports providing.
		/// <para>
		/// Used by the Inspector to populate dropdown list of compatible types.
		/// </para>
		/// </summary>
		/// <returns></returns>
		IEnumerable<Type> IValueByTypeProvider.GetSupportedValueTypes()
		{
			#if UNITY_EDITOR
			if(wrappedObjectType.Value is not { } type)
			{
				if(wrapped is null)
				{
					yield break;
				}

				type = wrapped.GetType();
			}

			yield return type;

			if(type.IsInterface)
			{
				foreach(var implementingType in TypeUtility.GetImplementingTypes(type))
				{
					yield return implementingType;
				}
			}
			else if(!type.IsValueType)
			{
				foreach(var derivedType in TypeUtility.GetDerivedTypes(type))
				{
					yield return derivedType;
				}

				foreach(var implementedType in type.GetInterfaces().Where(IncludeInterface))
				{
					yield return implementedType;
				}
			}
			#else
			yield break;
			#endif
		}

		#if UNITY_EDITOR
		void OnValidate() => EditorApplication.delayCall += ValidateOnMainThread;

		void ValidateOnMainThread()
		{
			if(!this || Application.isPlaying)
			{
				return;
			}

			foreach(var services in gameObject.GetComponents<Services>())
			{
				foreach(var serviceDefinition in services.providesServices)
				{
					if(ReferenceEquals(serviceDefinition.service, this))
					{
						#if DEV_MODE && DEBUG_ENABLED
						Debug.Log($"Hiding ServiceWrapper component on '{gameObject.name}' because it is referenced by a Services component.");
						#endif
						hideFlags = HideFlags.HideInInspector;
						return;
					}
				}
			}

			#if DEV_MODE
			Debug.Log($"Removing ServiceWrapper component from '{gameObject.name}' because no Services component on the same GameObject references it.", gameObject);
			#endif

			Undo.DestroyObjectImmediate(this);
		}

		MonoScript wrappedObjectScript;
		Type wrappedObjectScriptType;
		[SerializeReference] object wrappedSerializable; // only used for drawing purposes

		MonoScript WrappedObjectScript
		{
			get
			{
				if(wrappedObjectType != wrappedObjectScriptType)
				{
					wrappedObjectScript = Find.Script(wrappedObjectType);
					wrappedObjectScriptType = wrappedObjectType;
				}

				return wrappedObjectScript;
			}
		}

		internal void DrawGUI(Rect serviceRect, SerializedProperty serviceProperty)
		{
			var script = WrappedObjectScript;
			if(script)
			{
				var set = EditorGUI.ObjectField(serviceRect, script, typeof(MonoScript), false);
				if(!ReferenceEquals(set, script))
				{
					serviceProperty.objectReferenceValue = set;
				}
			}
			else
			{
				EditorGUI.PropertyField(serviceRect, serviceProperty, GUIContent.none);
			}
		}

		SerializedObject serializedObject;
		SerializedProperty wrappedSerializableProperty;
		internal void DrawStateGUI()
		{
			wrappedSerializable = wrapped;
			try
			{
				EditorGUI.BeginChangeCheck();
				serializedObject ??= new(this);
				wrappedSerializableProperty ??= serializedObject.FindProperty(nameof(wrappedSerializable));
				EditorGUILayout.PropertyField(wrappedSerializableProperty, new(TypeUtility.ToStringNicified(wrappedObjectType)), true);
				if(EditorGUI.EndChangeCheck() && (!Application.isPlaying || AssetDatabase.Contains(gameObject)))
				{
					serializedObject.ApplyModifiedProperties();
					wrapped = wrappedSerializable;
					wrappedObjectSerializedState = JsonUtility.ToJson(wrappedSerializable);
				}
			}
			finally
			{
				wrappedSerializable = null;
			}
		}

		static bool IncludeInterface(Type type) => type.IsGenericType ? !ignoredGenericInterfaces.Contains(type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition()) : !ignoredNonGenericInterfaces.Contains(type);

		static readonly HashSet<Type> ignoredNonGenericInterfaces = new()
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
			typeof(EditorOnly.IInitializableEditorOnly),
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
			typeof(System.Collections.IEnumerable),
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

		static readonly HashSet<Type> ignoredGenericInterfaces = new()
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
		#endif
	}
}