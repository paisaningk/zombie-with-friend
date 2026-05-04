using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using static Sisus.Init.FlagsValues;

namespace Sisus.Init
{
	/// <summary>
	/// Represents the result of <see cref="INullGuard.EvaluateNullGuard"/>.
	/// </summary>
	/// <remarks>
	/// <see cref="IValueProvider">value providers</see> can implement <see cref="NullGuardResult"/>
	/// to inform whether they will be able to provide a value to a given client at runtime.
	/// </remarks>
	public readonly struct NullGuardResult : IEquatable<NullGuardResult>
	{
		public static readonly NullGuardResult Passed = new();

		public NullGuardResultType Type { get; }
		[NotNull] public string Message => message ?? "";
		private readonly string message;

		private NullGuardResult(string message, NullGuardResultType type = NullGuardResultType.Error)
		{
			this.message = message;
			Type = type;
		}

		/// <summary>
		/// Creates a warning about an unresolvable dependency.
		/// </summary>
		public static NullGuardResult Warning(string message) => new(message, NullGuardResultType.Warning);
		
		/// <summary>
		/// Creates an error about an unresolvable dependency.
		/// </summary>
		public static NullGuardResult Error(string message) => new(message, NullGuardResultType.Error);

		/// <summary>
		/// Creates a warning box about an issue that will prevent initialization.
		/// </summary>
		public static NullGuardResult WarningBox(string message) => new(message, NullGuardResultType.Warning | NullGuardResultType.DrawAsHelpBox);

		/// <summary>
		/// Creates an error box about an issue that will prevent initialization.
		/// </summary>
		public static NullGuardResult ErrorBox(string message) => new(message, NullGuardResultType.Error | NullGuardResultType.DrawAsHelpBox);

		public static NullGuardResult ValueNotFound(string message) => new(message, Application.isPlaying ? NullGuardResultType.Error : NullGuardResultType.Warning);

		/// <summary>
		/// An exception was encountered while trying to retrieve one or more arguments.
		/// </summary>
		public static NullGuardResult Exception(Exception exception) => new(exception.ToString());
		public static NullGuardResult Exception(string exceptionMessage) => new(exceptionMessage);

		internal static NullGuardResult ClientHidesAwake => new("The component contains an <color=#ffd100><b>Awake</b></color> method, hiding the one in the base class. Override the <color=#ffd100><b>OnAwake</b></color> method instead.", NullGuardResultType.Warning | NullGuardResultType.DrawAsHelpBox);

		public static NullGuardResult InvalidValueProviderState => new("Invalid configuration.");
		public static NullGuardResult ClientNotSupported => new("Can not provide a value for this client.");
		public static NullGuardResult TypeNotSupported => new("Requested value type is not supported.");
		public static NullGuardResult ValueMissing => new("The value is missing.");
		public static NullGuardResult ValueProviderValueMissing = new("The value is missing.");
		public static NullGuardResult ValueProviderException = new("An exception occurred while trying to acquire the value.");
		public static NullGuardResult ClientException = new("An exception was thrown by the client while trying to resolve the value.");
		public static NullGuardResult ValueProviderValueNullInEditMode = new("The value is missing in edit mode, but it might be available at runtime.");

		public override string ToString() => message ?? "";
		public static bool operator == (NullGuardResult x, NullGuardResult y) => x.Equals(y);
		public static bool operator != (NullGuardResult x, NullGuardResult y) => !x.Equals(y);
		public override int GetHashCode() => message?.GetHashCode() ?? 0;
		public override bool Equals(object obj) => obj is NullGuardResult other && Equals(other);
		public bool Equals(NullGuardResult other) => Type switch
		{
			NullGuardResultType.Passed => other.Type is NullGuardResultType.Passed,
			NullGuardResultType.Warning => other.Type is NullGuardResultType.Warning && (string.IsNullOrEmpty(message) ? string.IsNullOrEmpty(other.message) : string.Equals(message, other.message)),
			NullGuardResultType.Error => other.Type == NullGuardResultType.Error && (string.IsNullOrEmpty(message) ? string.IsNullOrEmpty(other.message) : string.Equals(message, other.message)),
			_ => false
		};
	}

	/// <summary>
	/// Specifies the different types of results that <see cref="INullGuard.EvaluateNullGuard"/> can return.
	/// </summary>
	[Flags]
	public enum NullGuardResultType : byte
	{
		Passed = _0,
		Warning = _1,
		Error = _2,

		/// <summary>
		/// Specifies whether a Help Box should be drawn in the Inspector to warn about the null argument guard failure,
		/// or if showing a single generic warning about missing dependencies is sufficient.
		/// </summary>
		DrawAsHelpBox = _3
	}
}