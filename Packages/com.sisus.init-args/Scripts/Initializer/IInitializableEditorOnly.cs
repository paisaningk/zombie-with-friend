#if UNITY_EDITOR
using System.Diagnostics.CodeAnalysis;
using Sisus.Init.Internal;

namespace Sisus.Init.EditorOnly
{
	public interface IInitializableEditorOnly : IInitializable
	{
		[MaybeNull]
		IInitializer Initializer { get; set; }

		bool CanInitSelfWhenInactive { get; }

		InitState InitState { get; }
		string NullGuardFailedMessage { get => ""; set { } }
	}
}
#endif