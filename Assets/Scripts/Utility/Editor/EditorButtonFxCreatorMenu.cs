#if UNITY_EDITOR
using GameUI.Component;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
namespace Utility.Editor
{
    public static class EditorButtonFxCreatorMenu
    {
        private static TMP_DefaultControls.Resources sStandardResources;

        private const string KStandardSpritePath = "UI/Skin/UISprite.psd";
        private const string KBackgroundSpritePath = "UI/Skin/Background.psd";
        private const string KInputFieldBackgroundPath = "UI/Skin/InputFieldBackground.psd";
        private const string KKnobPath = "UI/Skin/Knob.psd";
        private const string KCheckmarkPath = "UI/Skin/Checkmark.psd";
        private const string KDropdownArrowPath = "UI/Skin/DropdownArrow.psd";
        private const string KMaskPath = "UI/Skin/UIMask.psd";

        private const float KWidth = 160f;
        private const float KThickHeight = 30f;

        private static readonly Vector2 SThickElementSize = new Vector2(KWidth, KThickHeight);

        private static TMP_DefaultControls.Resources GetStandardResources()
        {
            if (sStandardResources.standard == null)
            {
                sStandardResources.standard = AssetDatabase.GetBuiltinExtraResource<Sprite>(KStandardSpritePath);
                sStandardResources.background = AssetDatabase.GetBuiltinExtraResource<Sprite>(KBackgroundSpritePath);
                sStandardResources.inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>(KInputFieldBackgroundPath);
                sStandardResources.knob = AssetDatabase.GetBuiltinExtraResource<Sprite>(KKnobPath);
                sStandardResources.checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>(KCheckmarkPath);
                sStandardResources.dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>(KDropdownArrowPath);
                sStandardResources.mask = AssetDatabase.GetBuiltinExtraResource<Sprite>(KMaskPath);
            }
            return sStandardResources;
        }

        [MenuItem("GameObject/UI (Canvas)/Button Fx", false, 2031)]
        public static void AddButton(MenuCommand menuCommand)
        {
            var go = CreateButton(GetStandardResources());

            // Override font size
            var textComponent = go.GetComponentInChildren<TMP_Text>();
            textComponent.fontSize = 24;

            PlaceUIElementRoot(go, menuCommand);
        }

        private static GameObject CreateButton(TMP_DefaultControls.Resources resources)
        {
            var buttonRoot = CreateUIElementRoot("ButtonFx", SThickElementSize);

            var childText = ObjectFactory.CreateGameObject("Text (TMP)", typeof(RectTransform));

            SetParentAndAlign(childText, buttonRoot);

            var image = AddComponent<Image>(buttonRoot);
            image.sprite = resources.standard;
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, 1f);

            var bt = AddComponent<ButtonFx>(buttonRoot);
            SetDefaultColorTransitionValues(bt);

            var text = AddComponent<TextMeshProUGUI>(childText);
            text.text = "Button";
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(50f / 255f, 50f / 255f, 50f / 255f, 1f);

            bt.Text = text;

            var textRectTransform = childText.GetComponent<RectTransform>();
            textRectTransform.anchorMin = Vector2.zero;
            textRectTransform.anchorMax = Vector2.one;
            textRectTransform.sizeDelta = Vector2.zero;

            return buttonRoot;
        }


        private static GameObject CreateUIElementRoot(string name, Vector2 size)
        {
            GameObject root;

            #if UNITY_EDITOR
            root = ObjectFactory.CreateGameObject(name, typeof(RectTransform));
            var rt = root.GetComponent<RectTransform>();
            rt.sizeDelta = size;
            #else
            root = new GameObject(name);
            RectTransform rectTransform = root.AddComponent<RectTransform>();
            rectTransform.sizeDelta = size;
            #endif

            return root;
        }

        private static void SetDefaultColorTransitionValues(Selectable slider)
        {
            var colors = slider.colors;
            colors.highlightedColor = new Color(0.882f, 0.882f, 0.882f);
            colors.pressedColor = new Color(0.698f, 0.698f, 0.698f);
            colors.disabledColor = new Color(0.521f, 0.521f, 0.521f);
        }

        private static T AddComponent<T>(GameObject go) where T : Component
        {
#if UNITY_EDITOR
            return ObjectFactory.AddComponent<T>(go);
#else
            return go.AddComponent<T>();
#endif
        }

        private static void PlaceUIElementRoot(GameObject element, MenuCommand menuCommand)
        {
            var parent = menuCommand.context as GameObject;
            var explicitParentChoice = true;
            if (parent == null)
            {
                parent = GetOrCreateCanvasGameObject();
                explicitParentChoice = false;

                // If in Prefab Mode, Canvas has to be part of Prefab contents,
                // otherwise use Prefab root instead.
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                if (prefabStage != null && !prefabStage.IsPartOfPrefabContents(parent))
                    parent = prefabStage.prefabContentsRoot;
            }

            if (parent.GetComponentsInParent<Canvas>(true).Length == 0)
            {
                // Create canvas under context GameObject,
                // and make that be the parent which UI element is added under.
                var canvas = CreateNewUI();
                Undo.SetTransformParent(canvas.transform, parent.transform, "");
                parent = canvas;
            }

            GameObjectUtility.EnsureUniqueNameForSibling(element);

            SetParentAndAlign(element, parent);
            if (!explicitParentChoice) // not a context click, so center in sceneview
                SetPositionVisibleInSceneView(parent.GetComponent<RectTransform>(), element.GetComponent<RectTransform>());

            // This call ensure any change made to created Objects after they where registered will be part of the Undo.
            Undo.RegisterFullObjectHierarchyUndo(parent == null ? element : parent, "");

            // We have to fix up the undo name since the name of the object was only known after reparenting it.
            Undo.SetCurrentGroupName("Create " + element.name);

            Selection.activeGameObject = element;
        }

        private static void SetParentAndAlign(GameObject child, GameObject parent)
        {
            if (parent == null)
                return;

            Undo.SetTransformParent(child.transform, parent.transform, "");

            var rectTransform = child.transform as RectTransform;
            if (rectTransform)
            {
                rectTransform.anchoredPosition = Vector2.zero;
                var localPosition = rectTransform.localPosition;
                localPosition.z = 0;
                rectTransform.localPosition = localPosition;
            }
            else
            {
                child.transform.localPosition = Vector3.zero;
            }
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;

            SetLayerRecursively(child, parent.layer);
        }

        public static GameObject GetOrCreateCanvasGameObject()
        {
            var selectedGo = Selection.activeGameObject;

            // Try to find a gameobject that is the selected GO or one if its parents.
            var canvas = selectedGo != null ? selectedGo.GetComponentInParent<Canvas>() : null;
            if (IsValidCanvas(canvas))
                return canvas.gameObject;

            // No canvas in selection or its parents? Then use any valid canvas.
            // We have to find all loaded Canvases, not just the ones in main scenes.
            var canvasArray = StageUtility.GetCurrentStageHandle().FindComponentsOfType<Canvas>();
            for (var i = 0; i < canvasArray.Length; i++)
                if (IsValidCanvas(canvasArray[i]))
                    return canvasArray[i].gameObject;

            // No canvas in the scene at all? Then create a new one.
            return CreateNewUI();
        }

        public static GameObject CreateNewUI()
        {
            // Root for the UI
            var root = new GameObject("Canvas")
            {
                layer = LayerMask.NameToLayer("UI"),
            };
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            root.AddComponent<CanvasScaler>();
            root.AddComponent<GraphicRaycaster>();

            // Works for all stages.
            StageUtility.PlaceGameObjectInCurrentStage(root);
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (prefabStage != null)
            {
                root.transform.SetParent(prefabStage.prefabContentsRoot.transform, false);
            }

            Undo.RegisterCreatedObjectUndo(root, "Create " + root.name);

            // If there is no event system add one...
            // No need to place event system in custom scene as these are temporary anyway.
            // It can be argued for or against placing it in the user scenes,
            // but let's not modify scene user is not currently looking at.
            return root;
        }

        private static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            var t = go.transform;
            for (var i = 0; i < t.childCount; i++)
                SetLayerRecursively(t.GetChild(i).gameObject, layer);
        }


        private static void SetPositionVisibleInSceneView(RectTransform canvasRTransform, RectTransform itemTransform)
        {
            // Find the best scene view
            var sceneView = SceneView.lastActiveSceneView;

            if (sceneView == null && SceneView.sceneViews.Count > 0)
                sceneView = SceneView.sceneViews[0] as SceneView;

            // Couldn't find a SceneView. Don't set position.
            if (sceneView == null || sceneView.camera == null)
                return;

            // Create world space Plane from canvas position.
            var camera = sceneView.camera;
            var position = Vector3.zero;
            Vector2 localPlanePosition;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRTransform, new Vector2(camera.pixelWidth / 2, camera.pixelHeight / 2), camera,
                out localPlanePosition
            ))
            {
                // Adjust for canvas pivot
                localPlanePosition.x = localPlanePosition.x + canvasRTransform.sizeDelta.x * canvasRTransform.pivot.x;
                localPlanePosition.y = localPlanePosition.y + canvasRTransform.sizeDelta.y * canvasRTransform.pivot.y;

                localPlanePosition.x = Mathf.Clamp(localPlanePosition.x, 0, canvasRTransform.sizeDelta.x);
                localPlanePosition.y = Mathf.Clamp(localPlanePosition.y, 0, canvasRTransform.sizeDelta.y);

                // Adjust for anchoring
                position.x = localPlanePosition.x - canvasRTransform.sizeDelta.x * itemTransform.anchorMin.x;
                position.y = localPlanePosition.y - canvasRTransform.sizeDelta.y * itemTransform.anchorMin.y;

                Vector3 minLocalPosition;
                minLocalPosition.x = canvasRTransform.sizeDelta.x * (0 - canvasRTransform.pivot.x) + itemTransform.sizeDelta.x * itemTransform.pivot.x;
                minLocalPosition.y = canvasRTransform.sizeDelta.y * (0 - canvasRTransform.pivot.y) + itemTransform.sizeDelta.y * itemTransform.pivot.y;

                Vector3 maxLocalPosition;
                maxLocalPosition.x = canvasRTransform.sizeDelta.x * (1 - canvasRTransform.pivot.x) - itemTransform.sizeDelta.x * itemTransform.pivot.x;
                maxLocalPosition.y = canvasRTransform.sizeDelta.y * (1 - canvasRTransform.pivot.y) - itemTransform.sizeDelta.y * itemTransform.pivot.y;

                position.x = Mathf.Clamp(position.x, minLocalPosition.x, maxLocalPosition.x);
                position.y = Mathf.Clamp(position.y, minLocalPosition.y, maxLocalPosition.y);
            }

            itemTransform.anchoredPosition = position;
            itemTransform.localRotation = Quaternion.identity;
            itemTransform.localScale = Vector3.one;
        }

        private static bool IsValidCanvas(Canvas canvas)
        {
            if (canvas == null || !canvas.gameObject.activeInHierarchy)
                return false;

            // It's important that the non-editable canvas from a prefab scene won't be rejected,
            // but canvases not visible in the Hierarchy at all do. Don't check for HideAndDontSave.
            if (EditorUtility.IsPersistent(canvas) || (canvas.hideFlags & HideFlags.HideInHierarchy) != 0)
                return false;

            if (StageUtility.GetStageHandle(canvas.gameObject) != StageUtility.GetCurrentStageHandle())
                return false;

            return true;
        }
    }
}
#endif
