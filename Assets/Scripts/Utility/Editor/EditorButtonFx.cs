#if UNITY_EDITOR && ODIN_INSPECTOR
using GameUI.Component;
using Sirenix.OdinInspector.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
namespace Utility.Editor
{
    [CustomEditor(typeof(ButtonFx))] [CanEditMultipleObjects]
    public class EditorButtonFx : UnityEditor.UI.ButtonEditor
    {
        private PropertyTree tree;

        protected override void OnEnable()
        {
            base.OnEnable();
            tree = PropertyTree.Create(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            base.OnInspectorGUI();

            tree.UpdateTree();
            tree.BeginDraw(true);

            var btn = target as ButtonFx;
            if (btn)
            {
                if (btn.Text == null)
                {
                    if (GUILayout.Button("Add Text (TMP)"))
                    {
                        if (btn != null && btn.Text == null)
                        {
                            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                            go.transform.SetParent(btn.transform, false);

                            var tmp = go.GetComponent<TextMeshProUGUI>();
                            tmp.text = btn.name;
                            tmp.alignment = TextAlignmentOptions.Center;
                            tmp.color = Color.black;
                            tmp.fontSize = 24;
                            tmp.raycastTarget = false;
                            btn.Text = tmp;

                            EditorUtility.SetDirty(btn);
                        }
                    }
                }
                else
                {
                    DrawProp("Text");
                }
            }

            tree.GetPropertyAtPath("AdditionalTargetGraphics")?.Draw();

            tree.EndDraw();
            tree.ApplyChanges();

            // if (btn)
            // {
            //     if (btn.Text == null)
            //     {
            //         if (GUILayout.Button("Add Text (TMP)"))
            //         {
            //             if (btn != null && btn.Text == null)
            //             {
            //                 var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            //                 go.transform.SetParent(btn.transform, false);
            //                 
            //                 var tmp = go.GetComponent<TextMeshProUGUI>();
            //                 tmp.text = btn.name;
            //                 tmp.alignment = TextAlignmentOptions.Center;
            //                 tmp.color = Color.black;
            //                 tmp.fontSize = 24;
            //                 tmp.raycastTarget = false;
            //                 btn.Text = tmp;
            //             
            //                 EditorUtility.SetDirty(btn);
            //             }
            //         }
            //     }
            //     else
            //     {
            //         DrawProp("Text");
            //     }
            // }
            //
            // DrawProp("AdditionalTargetGraphics");
            //
            // //EditorGUILayout.Space(4);
            // //SirenixEditorGUI.Title("Fx Button", null, TextAlignment.Left, true);
            //     
            // SirenixEditorGUI.BeginBox();
            //     
            // DrawProp("PlayScaling");
            // if (GetBool("PlayScaling"))
            // {
            //     DrawProp("PressedScale");
            //     DrawProp("TweenSeconds");
            //     DrawProp("ease");
            // }
            //     
            // SirenixEditorGUI.EndBox();
            //
            // //EditorGUILayout.Space(4);
            //     
            // // // --- กลุ่ม SFX / VFX ---
            // // SirenixEditorGUI.BeginBox();
            // // GUILayout.Label("SFX / VFX", EditorStyles.boldLabel);
            // // DrawProp("AudioSource");
            // // DrawProp("HoverSfx");
            // // DrawProp("ClickSfx");
            // // DrawProp("ClickFx");
            // // DrawProp("FxSpawnRoot");
            // // SirenixEditorGUI.EndBox();
            //
            // // EditorGUILayout.Space(4);
            // //
            // // // ปุ่ม Preview (เล่นได้เฉพาะตอน Play Mode)
            // // using (new EditorGUI.DisabledScope(!Application.isPlaying))
            // // {
            // //     EditorGUILayout.BeginHorizontal();
            // //     if (GUILayout.Button("Preview Press")) CallOnTargets("PreviewPress");
            // //     if (GUILayout.Button("Preview Release")) CallOnTargets("PreviewRelease");
            // //     EditorGUILayout.EndHorizontal();
            // // }
            //
            // propertyTree.UpdateTree();
            //
            // propertyTree.ApplyChanges();
            //

            serializedObject.ApplyModifiedProperties();
        }

        private void DrawProp(string path)
        {
            var p = tree.GetPropertyAtPath(path);
            p?.Draw();
        }

        private bool GetBool(string path)
        {
            var p = tree.GetPropertyAtPath(path);
            return p?.ValueEntry.WeakSmartValue is bool b && b;
        }

        private void CallOnTargets(string methodName)
        {
            foreach (var t in targets)
            {
                var mb = t as MonoBehaviour;
                if (mb != null) mb.Invoke(methodName, 0f);
            }
        }
    }
}
#endif
