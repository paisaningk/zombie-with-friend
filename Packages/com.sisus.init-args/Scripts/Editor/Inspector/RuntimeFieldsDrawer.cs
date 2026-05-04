#define HIDE_RUNTIME_MEMBERS_IN_EDIT_MODE

//#define DEBUG_DEPENDENCY_WARNING_BOX
//#define DEBUG_FIELDS

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Sisus.Init.Internal;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace Sisus.Init
{
	using static NullExtensions;

	/// <summary>
	/// Object which can draw the non-serialized fields of a target.
	/// </summary>
	public sealed class RuntimeFieldsDrawer
	{
		private const BindingFlags AllDeclaredInstance = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
		private static readonly List<FieldInfo> fieldInfoBuilder = new();

		private static readonly Dictionary<Type, FieldInfo[]> nestedFields = new();
		private static readonly Dictionary<Type, Dictionary<string, bool>> targetUnfoldedness = new();

		private readonly object target;
		private readonly Type targetType;

		private readonly List<FieldInfo> rootFields;

		public string HeaderText { get; set; } = "Runtime State";

		public RuntimeFieldsDrawer([DisallowNull] object target, [AllowNull] Type baseType, Func<FieldInfo, bool> shouldDrawAsRuntimeField)
		{
			this.target = target;
			targetType = target.GetType();

			if(!targetUnfoldedness.ContainsKey(targetType))
			{
				targetUnfoldedness.Add(targetType, new());
			}

			rootFields = GetRuntimeFields(targetType, baseType, shouldDrawAsRuntimeField ?? ShouldDrawAsRuntimeField);
		}

		private bool GetUnfoldedness(string fieldName) => targetUnfoldedness[targetType].TryGetValue(fieldName, out bool cachedUnfolded) && cachedUnfolded;

		private static List<FieldInfo> GetRuntimeFields(Type type, [AllowNull] Type stopAtBaseType, Func<FieldInfo, bool> shouldDrawAsRuntimeField)
		{
			List<FieldInfo> fields = new();

			do
			{
				foreach(var field in type.GetFields(AllDeclaredInstance))
				{
					if(shouldDrawAsRuntimeField(field))
					{
						#if DEV_MODE && DEBUG_FIELDS
						Debug.Log("Field: " + field.Name);
						#endif

						fields.Add(field);
					}
				}

				type = type.BaseType;
			}
			while(type != stopAtBaseType && !TypeUtility.IsNullOrBaseType(type) && (stopAtBaseType == null || !stopAtBaseType.IsGenericTypeDefinition || !type.IsGenericType || type.GetGenericTypeDefinition() != stopAtBaseType));

			return fields;
		}

		internal static bool ShouldDrawAsRuntimeField([DisallowNull] FieldInfo field)
		{
			if(field.IsDefined(typeof(HideInInspector)) || field.IsDefined(typeof(SerializeField)) || field.IsDefined(typeof(SerializeReference)))
			{
				return false;
			}

			var fieldType = field.FieldType;
			return field.IsNotSerialized || field.IsInitOnly || field.IsPrivate || fieldType.IsAbstract || !fieldType.IsSerializable;
		}

		public void Draw()
		{
			#if DEV_MODE
			UnityEngine.Profiling.Profiler.BeginSample("RuntimeFieldsDrawer.InitializerGUI");
			#endif

			if(ShouldDraw())
			{
				EditorGUILayout.BeginHorizontal();
				GUILayout.Space(-14f);
				EditorGUILayout.BeginVertical();
				DrawRuntimeMembers();
				EditorGUILayout.EndVertical();
				EditorGUILayout.EndHorizontal();
			}

			#if DEV_MODE
			UnityEngine.Profiling.Profiler.EndSample();
			#endif
		}

		private bool ShouldDraw()
		{
			#if HIDE_RUNTIME_MEMBERS_IN_EDIT_MODE
			if(!Application.isPlaying)
			{
				return false;
			}

			var unityObject = target as Object;
			if(!unityObject)
			{
				unityObject = Find.WrapperOf(target) as Object;
				if(!unityObject)
				{
					return true;
				}
			}

			if(PrefabUtility.IsPartOfPrefabAsset(unityObject))
			{
				return false;
			}

			Component component = target as Component;
			return !component || !PrefabStageUtility.GetPrefabStage(component.gameObject);
			#else
			return true;
			#endif
		}

		private void DrawRuntimeMembers()
		{
			if(rootFields.Count == 0)
			{
				return;
			}

			GUILayout.Space(5f);

			GUI.enabled = false;

			if(HeaderText.Length > 0)
			{
				GUILayout.Label(HeaderText, EditorStyles.boldLabel);
			}

			var rect = GUILayoutUtility.GetLastRect();
			rect.y += rect.height;
			rect.height = 1f;
			var lineColor = GUI.skin.label.normal.textColor;
			lineColor.a = 0.3f;
			EditorGUI.DrawRect(rect, lineColor);
			GUI.enabled = true;

			foreach(var field in rootFields)
			{
				DrawField(target, field, 7);
			}
		}

		private void DrawField(object target, FieldInfo field, int maxDepth)
		{
			if(TryGetValue(target, field, out object value))
			{
				DrawField(field.Name, field.FieldType, value, maxDepth);
			}
		}

		private void DrawField(string variableName, Type fieldType, object value, int maxDepth)
		{
			if(maxDepth <= 0)
			{
				return;
			}

			var type = value == Null ? fieldType : value.GetType();
			var label = new GUIContent(ObjectNames.NicifyVariableName(variableName), TypeUtility.ToString(type));

			if(typeof(Object).IsAssignableFrom(type))
			{
				GUI.enabled = false;
				EditorGUILayout.ObjectField(label, (Object)value, fieldType, true);
				GUI.enabled = true;
				return;
			}

			if(value is null)
			{
				GUI.enabled = false;
				EditorGUILayout.LabelField(label: label, label2: new("null"));
				GUI.enabled = true;
				return;
			}

			if(type.IsPrimitive)
			{
				GUI.enabled = false;
				DrawPrimitiveField(value, type, label);
				GUI.enabled = true;
				return;
			}

			if(type == typeof(string))
			{
				GUI.enabled = false;
				EditorGUILayout.TextField(label, (string)value);
				GUI.enabled = true;
				return;
			}

			if(type.IsEnum)
			{
				GUI.enabled = false;
				EditorGUILayout.EnumPopup(label, (Enum)value);
				GUI.enabled = true;
				return;
			}

			if(type == typeof(Vector3))
			{
				GUI.enabled = false;
				EditorGUILayout.Vector3Field(label, (Vector3)value);
				GUI.enabled = true;
				return;
			}

			if(type == typeof(Vector2))
			{
				GUI.enabled = false;
				EditorGUILayout.Vector2Field(label, (Vector2)value);
				GUI.enabled = true;
				return;
			}

			if(type == typeof(Vector2Int))
			{
				GUI.enabled = false;
				EditorGUILayout.Vector2IntField(label, (Vector2Int)value);
				GUI.enabled = true;
				return;
			}

			if(type == typeof(Vector3Int))
			{
				GUI.enabled = false;
				EditorGUILayout.Vector3IntField(label, (Vector3Int)value);
				GUI.enabled = true;
				return;
			}

			if(type == typeof(Vector4))
			{
				GUI.enabled = false;
				EditorGUILayout.Vector4Field(label, (Vector4)value);
				GUI.enabled = true;
				return;
			}

			if(type == typeof(Rect))
			{
				GUI.enabled = false;
				EditorGUILayout.RectField(label, (Rect)value);
				GUI.enabled = true;
				return;
			}

			if(typeof(IEnumerable).IsAssignableFrom(type))
			{
				if(!DrawFoldout(label))
				{
					return;
				}

				EditorGUI.indentLevel++;

				var enumerable = value as IEnumerable;
				Type elementType = null;
				if(type.HasElementType)
				{
					elementType = type.GetElementType();
				}

				int index = 0;
				foreach(var elementValue in enumerable)
				{
					if(elementType is null)
					{
						if(elementValue is null)
						{
							GUI.enabled = false;
							EditorGUILayout.LabelField(label: label, label2: new("null"));
							GUI.enabled = true;
							continue;
						}

						elementType = elementValue.GetType();
					}

					DrawField("[" + index + "]", elementType, elementValue, maxDepth - 1);
					index++;
				}

				EditorGUI.indentLevel--;

				return;
			}

			if(type == typeof(DateTime) || type == typeof(TimeSpan))
			{
				GUI.enabled = false;
				EditorGUILayout.LabelField(label: label, label2: new(value.ToString()));
				GUI.enabled = true;
				return;
			}

			if(typeof(Delegate).IsAssignableFrom(type))
			{
				DrawDelegateField(label, (Delegate)value);
				return;
			}

			if(typeof(UnityEventBase).IsAssignableFrom(type))
			{
				DrawUnityEventField(label, (UnityEventBase)value);
				return;
			}

			var dataSetFields = GetFields(type);
			int count = dataSetFields.Length;

			if(variableName.StartsWith("[") && variableName.EndsWith("]") && TryGetValue(value, dataSetFields[0], out object firstNestedValue) && firstNestedValue != Null)
			{
				string firstNestedValueText = firstNestedValue.ToString();
				var firstNestedValueType = firstNestedValue.GetType();
				if(firstNestedValueText == firstNestedValueType.FullName)
				{
					firstNestedValueText = TypeUtility.ToString(firstNestedValueType);
				}

				if(firstNestedValueText.Length > 0)
				{
					label.text += " " + firstNestedValue;
				}
			}

			if(count == 0)
			{
				EditorGUILayout.LabelField(label: label, label2: GUIContent.none);
				return;
			}

			if(!DrawFoldout(label))
			{
				return;
			}

			EditorGUI.indentLevel++;

			for(int i = 0; i < count; i++)
			{
				DrawField(value, dataSetFields[i], maxDepth - 1);
			}

			EditorGUI.indentLevel--;
		}

		private void DrawUnityEventField(GUIContent label, UnityEventBase unityEvent)
		{
			if(unityEvent is null)
			{
				EditorGUILayout.LabelField(label: label, label2: new("null"));
				return;
			}

			if(!DrawFoldout(label))
			{
				return;
			}

			EditorGUI.indentLevel++;

			var callsField = typeof(UnityEventBase).GetField("m_Calls", AllDeclaredInstance);
			var invokableCallList = callsField.GetValue(unityEvent);
			var invokableCallListType = invokableCallList.GetType();
			FieldInfo persistentCallsField = invokableCallListType.GetField("m_PersistentCalls", AllDeclaredInstance);
			var persistentCalls = persistentCallsField.GetValue(invokableCallList) as ICollection;
			if(persistentCalls.Count > 0)
			{
				DrawField(persistentCallsField.Name, persistentCallsField.FieldType, persistentCalls, 7);
			}

			FieldInfo runtimeCallsField = invokableCallListType.GetField("m_RuntimeCalls", AllDeclaredInstance);
			var runtimeCalls = runtimeCallsField.GetValue(invokableCallList) as ICollection;
			if(runtimeCalls.Count > 0)
			{
				DrawField(runtimeCallsField.Name, runtimeCallsField.FieldType, runtimeCalls, 7);
			}

			EditorGUI.indentLevel--;
		}

		private void DrawDelegateField(GUIContent label, Delegate @delegate)
		{
			if(@delegate is null)
			{
				EditorGUILayout.LabelField(label: label, label2: new("null"));
				return;
			}

			if(!DrawFoldout(label))
			{
				return;
			}

			EditorGUI.indentLevel++;

			foreach(Delegate invocationItem in @delegate.GetInvocationList())
			{
				var method = invocationItem.Method;

				if(method is null)
				{
					EditorGUILayout.LabelField(label: "{ }", label2: "");
					continue;
				}

				var target = invocationItem.Target;
				bool isAnonymous;
				string methodName;
				Type targetType;

				if(target != null)
				{
					methodName = method.Name;

					isAnonymous = methodName[0] == '<';
					if(isAnonymous)
					{
						string methodOrigin = methodName.Substring(1, methodName.IndexOf('>') - 1);
						methodName = string.Concat("Anonymous Method (", methodOrigin, ")");
					}

					if(target is Object unityObject)
					{
						DrawDelegateInvocationListItem(unityObject, methodName);
						continue;
					}

					if(Find.WrapperOf(target) is Object wrapper)
					{
						DrawDelegateInvocationListItem(wrapper, methodName);
						continue;
					}

					EditorGUILayout.LabelField(label: target.GetType().Name, label2: methodName);
					continue;
				}

				targetType = method.ReflectedType;
				methodName = method.Name;

				isAnonymous = methodName[0] == '<';
				if(isAnonymous)
				{
					string methodOrigin = methodName.Substring(1, methodName.IndexOf('>') - 1);
					methodName = string.Concat("Anonymous Method (", methodOrigin, ")");
				}

				EditorGUILayout.LabelField(label: targetType.Name, label2: methodName);
			}

			EditorGUI.indentLevel--;
		}

		private static void DrawDelegateInvocationListItem(Object unityObject, string methodName)
		{
			GUI.enabled = false;
			GUILayout.BeginHorizontal();
			EditorGUILayout.ObjectField(unityObject, unityObject.GetType(), true);
			int indentWas = EditorGUI.indentLevel;
			EditorGUI.indentLevel = 0;
			EditorGUILayout.LabelField(methodName);
			EditorGUI.indentLevel = indentWas;
			GUILayout.EndHorizontal();
			GUI.enabled = true;
		}

		private bool DrawFoldout(GUIContent label)
		{
			bool wasUnfolded = GetUnfoldedness(label.text);
			GUI.color = new Color(1f, 1f, 1f, 0.5f);
			bool setUnfolded = EditorGUILayout.Foldout(wasUnfolded, label);
			GUI.color = Color.white;
			if(wasUnfolded != setUnfolded)
			{
				targetUnfoldedness[targetType][label.text] = setUnfolded;
			}

			return setUnfolded;
		}

		private static void DrawPrimitiveField(object value, Type type, GUIContent label)
		{
			switch(Type.GetTypeCode(type))
			{
				case TypeCode.Boolean:
					EditorGUILayout.Toggle(label, (bool)value);
					return;
				case TypeCode.Byte:
					EditorGUILayout.IntField(label, (byte)value);
					return;
				case TypeCode.Char:
					EditorGUILayout.TextField(label, new string(new char[] { (char)value }));
					return;
				case TypeCode.DBNull:
					EditorGUILayout.LabelField(label, "null");
					break;
				case TypeCode.Decimal:
					EditorGUILayout.DoubleField(label, decimal.ToDouble((decimal)value));
					return;
				case TypeCode.Double:
					EditorGUILayout.DoubleField(label, (double)value);
					return;
				case TypeCode.Int16:
					EditorGUILayout.IntField(label, (short)value);
					return;
				case TypeCode.Int32:
					EditorGUILayout.IntField(label, (int)value);
					return;
				case TypeCode.Int64:
					EditorGUILayout.DoubleField(label, (long)value);
					return;
				case TypeCode.SByte:
					EditorGUILayout.IntField(label, (sbyte)value);
					return;
				case TypeCode.Single:
					EditorGUILayout.FloatField(label, (float)value);
					return;
				case TypeCode.UInt16:
					EditorGUILayout.IntField(label, (ushort)value);
					return;
				case TypeCode.UInt32:
					EditorGUILayout.DoubleField(label, (uint)value);
					return;
				case TypeCode.UInt64:
					EditorGUILayout.DoubleField(label, (ulong)value);
					return;
				default:
					return;
			}
		}

		private static FieldInfo[] GetFields(Type type)
		{
			if(nestedFields.TryGetValue(type, out var fields))
			{
				return fields;
			}

			for(var t = type; t != null; t = t.BaseType)
			{
				fieldInfoBuilder.AddRange(t.GetFields(AllDeclaredInstance));
			}

			fields = fieldInfoBuilder.ToArray();
			fieldInfoBuilder.Clear();
			nestedFields.Add(type, fields);
			return fields;
		}

		private static bool TryGetValue(object owner, FieldInfo field, out object value)
		{
			if(owner is null)
			{
				value = null;
				return false;
			}

			value = field.GetValue(owner);
			return true;
		}
	}
}