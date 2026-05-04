namespace Sisus.Init
{
	/// <summary>
	/// Specifies the different types of value providers that can be used to provide a value of a particular type.
	/// </summary>
	internal enum ServiceProviderType
	{
		None = 0,
		ServiceInitializer,
		ServiceInitializerAsync,
		Initializer,
		Wrapper,
		IValueProviderT,
		IValueProviderAsyncT,
		IValueByTypeProvider,
		IValueByTypeProviderAsync,
		IValueProvider,
		IValueProviderAsync
	}
}