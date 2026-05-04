#if UNITY_EDITOR
using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using Sisus.Init.Internal;
using UnityEditor;
#endif
using System.Linq;
using UnityEngine;

namespace Sisus.Init
{
	[CreateAssetMenu(fileName = "Base Class Generator", menuName = "Init(args)/Base Class Generator")]
	public sealed class BaseClassGenerator : ScriptableObject
	{
		public string[] UsingStatements;
		public string ClassName = "MyBehaviour";
		public string DerivesFrom = "MyBehaviour";
		public string PreprocessorDirective = "";
		[Range(1, 12)]
		public int MaxArgumentCount = 12;

		#if UNITY_EDITOR
		[ContextMenu("Generate")]
		public void Generate()
		{
			if(string.IsNullOrWhiteSpace(ClassName))
			{
				Debug.LogWarning("BaseClassGenerator: 'Class Name' has not been specified.", this);
				return;
			}

			var baseScript = Find.Script(typeof(MonoBehaviourBase));
			var fromPath = AssetDatabase.GetAssetPath(baseScript);
			var fromDirectory = Path.GetDirectoryName(fromPath);
			var toDirectory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(this));
			var toPath = Path.Combine(toDirectory, ClassName + "Base.cs");

			AssetDatabase.StartAssetEditing();

			var derivesFromType = GetDerivesFromType();
			CopyAndConvertBase(fromPath, toPath, derivesFromType);

			for(var i = 1; i <= MaxArgumentCount; i++)
			{
				fromPath = Path.Combine(fromDirectory , "MonoBehaviourT" + i + ".cs");
				toPath = Path.Combine(toDirectory , ClassName + "T" + i + ".cs");
				CopyAndConvert(fromPath, toPath, derivesFromType);
			}

			AssetDatabase.StopAssetEditing();
		}

		void CopyAndConvertBase(string fromPath, string toPath, [MaybeNull] Type derivesFromType)
		{
			var text = File.ReadAllText(fromPath);

			text = text.Replace("MonoBehaviour", DerivesFrom, StringComparison.Ordinal);

			// MonoBehaviourBase -> BaseClassName + "Base" -> ClassName + "Base"
			text = text.Replace(DerivesFrom + "Base", ClassName + "Base", StringComparison.Ordinal);

			text = AddUsingStatements(text);
			text = AddPreprocessorDirective(text);

			// Handle executing base.Awake when one is defined in the derivesFrom type.
			if(GetParameterlessMethod(derivesFromType, "Awake") is { } existingAwakeMethod)
			{
				const string oldCode = "\t\tprotected async void Awake()\r\n" +
									   "\t\t{";

				var newAccessModifiers = GetAccessModifiers(existingAwakeMethod);
				string newCode;
				if(existingAwakeMethod.IsAbstract)
				{
					newCode = "\t\t" + newAccessModifiers + " override async void Awake()\r\n" +
							  "\t\t{";
				}
				else if(existingAwakeMethod.IsVirtual)
				{
					newCode = "\t\t" + newAccessModifiers + " override async void Awake()\r\n" +
							  "\t\t{\r\n" +
							  "\t\t\tbase.Awake();";
				}
				else if(existingAwakeMethod.IsPrivate)
				{
					Debug.LogWarning($"BaseClassGenerator: The Awake method in '{derivesFromType}' is marked as private, so it cannot be overridden. The generated Awake method will not call base.Awake().", this);
					newCode = "\t\tprotected async void Awake()\r\n" +
							  "\t\t{\r\n" +
							  $"\t\t\tTODO: Make {derivesFromType.Name}.Awake() protected and add base.Awake(); here\r\n";
				}
				else
				{
					newCode = "\t\tprotected async new void Awake()\r\n" +
							  "\t\t{\r\n" +
							  "\t\t\tbase.Awake();";
				}

				text = text.Replace(oldCode, newCode, StringComparison.Ordinal);
			}

			File.WriteAllText(toPath, text);
			AssetDatabase.ImportAsset(toPath);
		}

		void CopyAndConvert(string fromPath, string toPath, [MaybeNull] Type derivesFromType)
		{
			var text = File.ReadAllText(fromPath);

			text = text.Replace("MonoBehaviour", ClassName, StringComparison.Ordinal);

			text = AddUsingStatements(text);
			text = AddPreprocessorDirective(text);

			// Handle executing base.Reset when one is defined in the derivesFrom type.
			if(GetParameterlessMethod(derivesFromType, "Reset") is { } existingResetMethod)
			{
				const string oldCode = "\t\tprivate protected void Reset()\r\n" +
									   "\t\t{";

				var newAccessModifiers = GetAccessModifiers(existingResetMethod);
				string newCode;
				if(existingResetMethod.IsAbstract)
				{
					newCode = "\t\t" + newAccessModifiers + " override void Reset()\r\n" +
							  "\t\t{";
				}
				else if(existingResetMethod.IsVirtual)
				{
					newCode = "\t\t" + newAccessModifiers + " override void Reset()\r\n" +
							  "\t\t{\r\n" +
							  "\t\t\tbase.Reset();";
				}
				else if(existingResetMethod.IsPrivate)
				{
					Debug.LogWarning($"BaseClassGenerator: The Reset method in '{derivesFromType}' is marked as private, so it cannot be overridden. The generated Reset method will not call base.Reset().", this);
					newCode = "\t\tprivate protected void Reset()\r\n" +
							  "\t\t{\r\n" +
							  $"\t\t\tTODO: Make {derivesFromType.Name}.Reset() protected and add base.Reset(); here\r\n";
				}
				else
				{
					newCode = "\t\tprivate protected new void Reset()\r\n" +
							  "\t\t{\r\n" +
							  "\t\t\tbase.Reset();";
				}

				text = text.Replace(oldCode, newCode, StringComparison.Ordinal);
			}

			File.WriteAllText(toPath, text);
			AssetDatabase.ImportAsset(toPath);
		}
		
		string AddUsingStatements(string text)
		{
			if(UsingStatements.Length is 0)
			{
				return text;
			}

			for(var i = 0; i < UsingStatements.Length; i++)
			{
				text = $"using {UsingStatements[i]};\r\n" + text;
			}

			return text;
		}

		string AddPreprocessorDirective(string text)
		{
			if(string.IsNullOrEmpty(PreprocessorDirective))
			{
				return text;
			}

			return $"#if {PreprocessorDirective}\r\n{text}\r\n#endif";
		}
		
		[return: MaybeNull]
		Type GetDerivesFromType()
		{
			if(string.IsNullOrEmpty(DerivesFrom))
			{
				Debug.LogWarning("BaseClassGenerator: 'Derives From' type name has not been specified.", this);
				return null;
			} 

			var matchesByTypeName = TypeUtility.GetDerivedTypes(typeof(MonoBehaviour)).Where(t => string.Equals(t.Name, DerivesFrom) && !t.IsSealed).ToArray();
			if(matchesByTypeName.Length is 0)
			{
				matchesByTypeName = TypeUtility.GetDerivedTypes(typeof(MonoBehaviour)).Where(t => string.Equals(t.FullName, DerivesFrom) && !t.IsSealed).ToArray();
				if(matchesByTypeName.Length is 0)
				{
					Debug.LogWarning($"BaseClassGenerator: Could not find any base type named '{DerivesFrom}' that derives from MonoBehaviour.", this);
					return null;
				}
			}

			if(matchesByTypeName.Length is 1)
			{
				return matchesByTypeName[0];
			}

			var matchesByFullName = matchesByTypeName.Where(t => Array.IndexOf(UsingStatements, t.Namespace) is not -1).ToArray();
			if(matchesByFullName.Length is 0)
			{
				Debug.LogWarning($"BaseClassGenerator: Found {matchesByTypeName.Length} types named '{DerivesFrom}' that derive from MonoBehaviour, but none in the specified namespaces. Unable to determine if type contains an Awake or Reset method definitions that should be executed.\n\nSearched For:\n{string.Join("\n", UsingStatements.Select(u => u + "." + DerivesFrom))}\n\nFound:\n{string.Join("\n", matchesByTypeName.Select(t => t.FullName))}", this);
				return null;
			}

			if(matchesByFullName.Length is 1)
			{
				return matchesByFullName[0];
			}

			Debug.LogWarning($"BaseClassGenerator: Found multiple types named '{DerivesFrom}' that derive from MonoBehaviour even when filtering by the specified namespaces.", this);
			return null;
		}

		[return: MaybeNull]
		static MethodInfo GetParameterlessMethod([MaybeNull] Type type, string methodName)
		{
			while (type != typeof(MonoBehaviour) && type is not null)
			{
				var method = type.GetMethod(
					methodName, 
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
					null,
					Type.EmptyTypes,
					null
				);

				if (method != null)
				{
					return method;
				}

				type = type.BaseType;
			}

			return null;
		}

		static string GetAccessModifiers(MethodInfo method)
		{
			if (method.IsPublic)
			{
				return "public";
			}

			if (method.IsFamily)
			{
				return "protected";
			}

			if (method.IsAssembly)
			{
				return "internal";
			}

			if (method.IsPrivate)
			{
				return "private";
			}

			if(method.IsFamilyOrAssembly)
			{
				return "protected internal";
			}

			Debug.LogWarning("BaseClassGenerator: Could not determine access modifiers for method: " + method);
			return "private";
		}
		#endif
	}
}