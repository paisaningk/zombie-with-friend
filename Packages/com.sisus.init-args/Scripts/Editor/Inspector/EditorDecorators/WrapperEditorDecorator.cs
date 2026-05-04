#define DEBUG_WRAPPED_SCRIPT

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Reflection;
using Sisus.Shared.EditorOnly;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Sisus.Init.EditorOnly.Internal
{
	/// <summary>
	/// Handles drawing the serialized fields of the wrapped object wrapped by a <see cref="Wrapper{T}"/>.
	/// </summary>
	internal sealed class WrapperEditorDecorator : InitializableEditorDecorator
	{
		private readonly Dictionary<string, bool> shouldDrawCache = new();

		protected override Type BaseTypeDefinition => typeof(Wrapper<>);
		private object WrappedObject => GetInitializable(target);

		public WrapperEditorDecorator(Editor decoratedEditor) : base(decoratedEditor) { }

		protected override object GetInitializable(Object inspectedTarget) => ((IWrapper)inspectedTarget).WrappedObject;

		[Pure]
		protected override RuntimeFieldsDrawer CreateRuntimeFieldsDrawer()
			=> WrappedObject is not null ? new(WrappedObject, typeof(object), ShouldDrawAsRuntimeField) : base.CreateRuntimeFieldsDrawer();

		protected override bool ShouldDrawAsRuntimeField([DisallowNull] FieldInfo field) => field.Name is "wrapped" || RuntimeFieldsDrawer.ShouldDrawAsRuntimeField(field);

		public override void OnAfterInspectorGUI()
		{
			if(!DecoratingDefaultOrOdinEditor)
			{
				base.OnAfterInspectorGUI();
				return;
			}

			var serializedObject = SerializedObject;
			if(serializedObject?.FindProperty("wrapped") is not { } wrappedObjectProperty)
			{
				base.OnAfterInspectorGUI();
				return;
			}

			if(!wrappedObjectProperty.Next(true))
			{
				wrappedObjectProperty.Dispose();
				base.OnAfterInspectorGUI();
				return;
			}

			serializedObject.Update();

			const float minLabelWidth = 120f;
			const float expandLabelWidthThreshold = 355f;
			EditorGUIUtility.labelWidth = EditorGUIUtility.currentViewWidth <= expandLabelWidthThreshold ? minLabelWidth : minLabelWidth + (EditorGUIUtility.currentViewWidth - expandLabelWidthThreshold) * 0.449f;

			try
			{
				do
				{
					if(!shouldDrawCache.TryGetValue(wrappedObjectProperty.propertyPath, out var shouldDraw))
					{
						shouldDraw = wrappedObjectProperty.GetMemberInfo()?.IsDefined(typeof(HideInInspector), false) is not true;
						shouldDrawCache[wrappedObjectProperty.propertyPath] = shouldDraw;
					}

					if(shouldDraw)
					{
						EditorGUILayout.PropertyField(wrappedObjectProperty, includeChildren: true);
					}
				}
				while(wrappedObjectProperty.Next(enterChildren: false));
			}
			finally
			{
				if(serializedObject.IsValid())
				{
					serializedObject.ApplyModifiedProperties();
				}
			}
			
			wrappedObjectProperty.Dispose();

			base.OnAfterInspectorGUI();
		}
	}
}