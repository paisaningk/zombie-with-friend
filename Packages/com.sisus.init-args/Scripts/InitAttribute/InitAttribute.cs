using System;

namespace Sisus.Init
{
	/// <summary>
	/// Specifies the initialization settings to use for all instances of the class to which it is attached.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This attribute should only be attached to types that derives from a <see cref="MonoBehaviour{T}"/> base class
	/// (either directly, or indirectly).
	/// </para>
	/// <para>
	/// Initializers can still be used to override initialization settings for individual instances of a class
	/// even if the class has the <see cref="InitAttribute"/>.
	/// </para>
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
	public class InitAttribute : Attribute
	{
		/// <summary>
		/// Specifies if the Init section should be drawn in the Inspector window or not.
		/// </summary>
		public bool HideInInspector { get; set; }

		/// <summary>
		/// <para>
		/// Specifies whether or not an initializer should be attached to the component by default when
		/// it is attached to a GameObject in the editor in Edit Mode, and if so, which type of initializer.
		/// </para>
		/// <para>
		/// If a value of <see langword="false"/> or <see langword="null"/> is assigned, then no initializer
		/// will be attached to the component by default.
		/// </para>
		/// <para>
		/// If a value of <see langword="true"/> is assigned, then the first initializer that is found
		/// is attached to the component by default.
		/// </para>
		/// <para>
		/// If a <see cref="Type"/> value is assigned, then an initializer of that type is attached to the
		/// component by default.
		/// </para>
		/// </summary>
		/// <exception cref="ArgumentException">
		/// Thrown if a value that is not a <see cref="Type"/>, <see langword="bool"/> or <see langword="null"/>
		/// is assigned to this property, of if the <see cref="Type"/> is not assignable to <see cref="IInitializer"/>.
		/// </exception>
		public object DefaultInitializer
		{
			get => addDefaultInitializer is false ? null : defaultInitializerType is not null ? defaultInitializerType : true;

			set
			{
				if(value is Type type)
				{
					defaultInitializerType = type;
					addDefaultInitializer = true;

					if(!typeof(IInitializer).IsAssignableFrom(type))
					{
						throw new ArgumentException($"The type {type.Name} is not assignable to {nameof(IInitializer)}.");
					}
				}
				else if(value is bool boolean)
				{
					addDefaultInitializer = boolean;
					defaultInitializerType = null;
				}
				else if(value is null)
				{
					addDefaultInitializer = false;
					defaultInitializerType = null;
				}
				else
				{
					throw new ArgumentException($"{nameof(DefaultInitializer)} only accepts a Type or a boolean, but was provided a value of type {value.GetType().Name}.");
				}
			}
		}

		public NullArgumentGuard NullArgumentGuard
		{
			get => nullArgumentGuard ?? NullArgumentGuard.All;
			set => nullArgumentGuard = value;
		}

		internal NullArgumentGuard? nullArgumentGuard;

		/// <summary>
		/// Specifies whether the client should wait for the services that it depends on to be available before becoming enabled.
		/// <para>
		/// If <see langword="false"/>, the client will only attempt once to receive the services that it depends on at the beginning of its Awake event.
		/// The client will become enabled without any delay, even if some of the services are missing.
		/// </para>
		/// <para>
		/// If <see langword="true"/>, the client will remain in disabled state and wait until all the services that it depends on have become available.
		/// </para>
		/// </summary>
		public bool WaitForServices
		{
			get => waitForServices is true;
			set => waitForServices = value;
		}
		
		internal bool? waitForServices { get; private set; }

		internal bool? addDefaultInitializer { get; private set; }
		internal Type defaultInitializerType { get; private set; }

		/// <summary>
		/// Assigning <see langword="false"/> to this property will hide the Init section in the Inspector window,
		/// turn off the <see cref="NullArgumentGuard"/>, disable <see cref="WaitForServices"/> and set
		/// <see cref="DefaultInitializer"/> to <see langword="null"/>.
		/// </summary>
		public bool Enabled
		{
			get => !HideInInspector;

			set
			{
				if(!value)
				{
					waitForServices = false;
					NullArgumentGuard = NullArgumentGuard.None;
					HideInInspector = true;
					addDefaultInitializer ??= false;
				}
				else
				{
					if(waitForServices is false)
					{
						waitForServices = null;
					}
					
					if(NullArgumentGuard is NullArgumentGuard.None)
					{
						NullArgumentGuard = NullArgumentGuard.All;
					}

					HideInInspector = false;
					addDefaultInitializer ??= true;
				}
			}
		}
	}
}