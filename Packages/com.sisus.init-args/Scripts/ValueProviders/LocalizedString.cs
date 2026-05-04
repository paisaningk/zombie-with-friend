#if UNITY_LOCALIZATION
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using UnityEngine;
using UnityEngine.Localization.Settings;
using static Sisus.Init.ValueProviders.ValueProviderUtility;

namespace Sisus.Init.Internal
{
	/// <summary>
	/// Provides a <see cref="string"/> that has been localized by Unity's Localization package.
	/// <para>
	/// Can be used to retrieve an Init argument at runtime.
	/// </para>
	/// </summary>
	#if !INIT_ARGS_DISABLE_VALUE_PROVIDER_MENU_ITEMS
	[ValueProviderMenu("Localized String", typeof(string), Order = 1, Tooltip = "Text will be localized at runtime for the active locale.")]
	#endif
	#if !INIT_ARGS_DISABLE_CREATE_ASSET_MENU_ITEMS
	[CreateAssetMenu(fileName = MENU_NAME, menuName = CREATE_ASSET_MENU_GROUP + MENU_NAME)]
	#endif
	internal sealed class LocalizedString : ScriptableObject, IValueProviderAsync<string>
	#if UNITY_EDITOR
	, INullGuard
	#endif
	{
		private const string MENU_NAME = "Localized String";

		[SerializeField]
		internal UnityEngine.Localization.LocalizedString value = new();

		[MaybeNull]
		public string Value
		{
			get
			{
				if(value.IsEmpty)
				{
					return null;
				}
				
				var getter = value.GetLocalizedStringAsync();
				return getter.IsDone ? getter.Result : null;
			}
		}

		/// <summary>
		/// Gets the translated string synchronously, blocking the thread until the load operation has finished.
		/// </summary>
		/// <remarks>
		/// Please note that this method is not supported on WebGL and can cause deadlocking
		/// if called during the Awake or OnEnable event functions.
		/// </remarks>
		/// <returns> The localized string for the currently selected locale. </returns>
		[return: MaybeNull]
		public string GetValueUnsafe() => value.IsEmpty ? null : value.GetLocalizedString();

		public async Awaitable<string> GetForAsync(Component client, CancellationToken cancellationToken = default) => value.IsEmpty ? null : await value.GetLocalizedStringAsync(cancellationToken).Task;

		public static implicit operator string(LocalizedString localizedString) => localizedString.Value;

		private void OnDestroy() => ((IDisposable)value).Dispose();

		#if UNITY_EDITOR
		private void OnValidate()
		{
			LocalizationSettings.SelectedLocaleChanged -= SelectedLocaleChanged;
			LocalizationSettings.SelectedLocaleChanged += SelectedLocaleChanged;

			value.RefreshString();
		}

		private static void SelectedLocaleChanged(UnityEngine.Localization.Locale locale) => InitInEditModeUtility.UpdateAll();

		NullGuardResult INullGuard.EvaluateNullGuard([AllowNull] Component client) => value.IsEmpty ? NullGuardResult.Error("Invalid localized string reference.") : NullGuardResult.Passed;
		#endif
	}
}
#endif