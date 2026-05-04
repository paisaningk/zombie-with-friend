using System;
using System.Runtime.CompilerServices;
using Sisus.Init.Serialization;
using Object = UnityEngine.Object;

namespace Sisus.Init.Internal
{
	/// <summary>
	/// Defines a single service that derives from <see cref="Object"/> as well
	/// as the defining type of the services which its clients can use to retrieving the service instance.
	/// <para>
	/// Used by both the <see cref="Services"/> components
	/// </para>
	/// </summary>
	[Serializable]
	internal sealed class ServiceDefinition
	{
		#pragma warning disable CS0649
		public Object service;

		public _Type definingType = new();
		#pragma warning restore CS0649

		public Type DefiningType
		{
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			get =>  definingType.Value;
		}

		private ServiceDefinition() { }

		public ServiceDefinition(Object service, _Type definingType)
		{
			this.service = service;	
			this.definingType = definingType;
		}

		public ServiceDefinition(Object service, Type definingType)
		{
			this.service = service;
			this.definingType = new(definingType);
		}

		internal int GetStateBasedHashCode() => service ? service.GetHashCode() ^ definingType.GetHashCode() : 0;
	}
}