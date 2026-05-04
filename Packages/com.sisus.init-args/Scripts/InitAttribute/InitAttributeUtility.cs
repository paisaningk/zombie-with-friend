using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

namespace Sisus.Init.Internal
{
	[EditorBrowsable(EditorBrowsableState.Never)]
	public static class InitAttributeUtility
	{
		private static readonly Dictionary<Type, InitAttribute> initAttributes = new();

		public static bool TryGet(Type type, out InitAttribute initAttribute)
		{
			if(initAttributes.TryGetValue(type, out initAttribute))
			{
				return initAttribute is not null;
			}

			initAttribute = type.GetCustomAttributes<InitAttribute>(inherit: false).FirstOrDefault();
			if(initAttribute is not null)
			{
				initAttributes.Add(type, initAttribute);
				return true;
			}

			initAttributes.Add(type, null);
			return false;
		}
	}
}