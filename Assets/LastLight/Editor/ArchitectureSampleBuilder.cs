using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LastLight.Editor
{
    public static class ArchitectureSampleBuilder
    {
        public const string Root = "Assets/LastLight/ArchitectureSample";
        public const string Boot = Root + "/Scenes/Bootstrap.unity";
        private static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        private static AddressableAssetGroup group;
        private static AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        [MenuItem("Tools/LastLight/Architecture/Generate Sample")]
        public static void Generate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("生成前退出 Play Mode。");
            for (int i = 0; i < SceneManager.sceneCount; ++i)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("请先保存当前场景；生成器不会覆盖未保存修改。");
            foreach (var folder in new[] { "Scenes", "Prefabs", "Configuration", "Materials" }) Directory.CreateDirectory(Root + "/" + folder);
            AssetDatabase.Refresh();
            if (Settings == null) throw new InvalidOperationException("项目缺少 Addressables 配置。");
            group = Settings.FindGroup("LastLight Architecture Local") ?? Settings.CreateGroup("LastLight Architecture Local", false, false, true, null, typeof(BundledAssetGroupSchema), typeof(ContentUpdateGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.BuildPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalBuildPath);
            schema.LoadPath.SetVariableByName(Settings, AddressableAssetSettings.kLocalLoadPath);
            schema.BundleMode = BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            var previous = SceneManager.GetActiveScene();
            var scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scratch);
                var definitions = new[]
                {
                    Definition("Menu", UILayer.BG, false, false, false),
                    Definition("HUD", UILayer.BG, false, false, false),
                    Definition("Pause", UILayer.Window, true, true, true),
                    Definition("Confirm", UILayer.Pop, true, true, true),
                    Definition("Loading", UILayer.Over, true, false, false, SystemLifetime.Application)
                };
                foreach (var definition in definitions) MakePanel(definition);
                var catalog = AssetDatabase.LoadAssetAtPath<PanelCatalog>(Root + "/Configuration/Panels.asset");
                if (catalog == null) { catalog = ScriptableObject.CreateInstance<PanelCatalog>(); AssetDatabase.CreateAsset(catalog, Root + "/Configuration/Panels.asset"); }
                catalog.Panels = definitions; EditorUtility.SetDirty(catalog);
                var globalPrefab = MakeRoot(catalog);
                MakeToken();
                MakeRecovery();
                EditorSceneManager.CloseScene(scratch, true);
                SceneManager.SetActiveScene(previous);
                MakeBoot(globalPrefab);
                foreach (var id in new[] { "Menu", "A", "B" }) MakeScene(id);
                AssetDatabase.SaveAssets(); Validate();
                Debug.Log("LastLight architecture sample generated. Entry: " + Boot);
            }
            finally
            {
                if (scratch.IsValid() && scratch.isLoaded) EditorSceneManager.CloseScene(scratch, true);
                if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
        }
        private static PanelDefinition Definition(string id, UILayer layer, bool modal, bool pause, bool closable, SystemLifetime lifetime = SystemLifetime.Scene)
            => new PanelDefinition { Id = id, Address = "LastLight/UI/" + id, Layer = layer, Modal = modal, Pause = pause, Closable = closable, Cache = true, Lifetime = lifetime };
        private static RectTransform Rect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false); return result;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero; rect.anchoredPosition3D = Vector3.zero;
            rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
        }
        private static void Box(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = size; rect.anchoredPosition = position; }
        private static Image Fill(RectTransform rect, Color color, bool raycast)
        { var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image; }
        private static Text Text(string name, Transform parent, string content, int size, Vector2 dimensions, Vector2 position)
        {
            var rect = Rect(name, parent); Box(rect, new Vector2(.5f, .5f), dimensions, position);
            var text = rect.gameObject.AddComponent<Text>(); text.font = Font; text.fontSize = size; text.text = content;
            text.color = new Color(.9f, .94f, .96f); text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            return text;
        }
        private static Button Button(Transform parent, string name, Vector2 dimensions, Vector2 position)
        {
            var rect = Rect(name, parent); Box(rect, new Vector2(.5f, .5f), dimensions, position);
            Fill(rect, new Color(.16f, .31f, .37f), true);
            var button = rect.gameObject.AddComponent<Button>();
            var label = Text("Label", rect, name, 22, dimensions - new Vector2(28, 0), Vector2.zero);
            label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        private static void MakePanel(PanelDefinition definition)
        {
            var rect = Rect(definition.Id, null); Stretch(rect);
            rect.gameObject.AddComponent<CanvasGroup>();
            if (definition.Modal) Fill(rect, new Color(.02f, .04f, .07f, .8f), true);
            var card = Rect("Content", rect);
            bool hud = definition.Id == "HUD";
            Box(card, hud ? new Vector2(0, 1) : new Vector2(.5f, .5f), new Vector2(570, 640), hud ? new Vector2(310, -350) : Vector2.zero);
            Fill(card, new Color(.045f, .09f, .14f, .97f), true);
            var title = Text("Title", card, definition.Id, 26, new Vector2(514, 70), new Vector2(0, 260));
            title.color = new Color(.99f, .77f, .35f);
            var body = Text("Body", card, "", 22, new Vector2(514, 180), new Vector2(0, 125));
            var buttons = new Button[5];
            for (int i = 0; i < buttons.Length; ++i) buttons[i] = Button(card, "Action " + i, new Vector2(514, 50), new Vector2(0, -10 - i * 59));
            rect.gameObject.AddComponent<SamplePanel>().Configure(title, body, buttons);
            string path = Root + "/Prefabs/" + definition.Id + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(rect.gameObject, path); UnityEngine.Object.DestroyImmediate(rect.gameObject); Address(path, definition.Address);
        }
        private static GlobalManager MakeRoot(PanelCatalog catalog)
        {
            var go = new GameObject("LastLight Global");
            go.AddComponent<AudioListener>(); // Additive 切换期间仍保持唯一监听器。
            var ui = go.AddComponent<UIManager>(); var global = go.AddComponent<GlobalManager>();
            var canvasRect = Rect("Canvas", go.transform); Stretch(canvasRect);
            var canvas = canvasRect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var roots = new RectTransform[4];
            for (int i = 0; i < 4; ++i)
            {
                roots[i] = Rect(((UILayer)i).ToString(), canvasRect); Stretch(roots[i]);
                var layerCanvas = roots[i].gameObject.AddComponent<Canvas>(); layerCanvas.overrideSorting = true; layerCanvas.sortingOrder = i * 100;
                roots[i].gameObject.AddComponent<GraphicRaycaster>();
            }
            ui.Configure(catalog, roots);
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); events.transform.SetParent(go.transform, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, Root + "/Prefabs/GlobalRoot.prefab");
            UnityEngine.Object.DestroyImmediate(go); return prefab.GetComponent<GlobalManager>();
        }
        private static void MakeToken()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "Pooled Token"; go.AddComponent<SampleToken>();
            string path = Root + "/Prefabs/Token.prefab"; PrefabUtility.SaveAsPrefabAsset(go, path); UnityEngine.Object.DestroyImmediate(go);
            Address(path, "LastLight/Prefabs/Token");
        }
        private static void MakeRecovery()
        {
            var rect = Rect("Startup Recovery", null); Stretch(rect); Fill(rect, new Color(.04f, .07f, .11f), true);
            var message = Text("Message", rect, "", 24, new Vector2(900, 400), new Vector2(0, 90));
            var retry = Button(rect, "Retry", new Vector2(400, 60), new Vector2(0, -190));
            var fallback = new GameObject("Recovery Input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            fallback.transform.SetParent(rect, false); fallback.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); fallback.SetActive(false);
            rect.gameObject.AddComponent<StartupRecoveryPanel>().Configure(message, retry, fallback);
            PrefabUtility.SaveAsPrefabAsset(rect.gameObject, Root + "/Prefabs/StartupRecovery.prefab"); UnityEngine.Object.DestroyImmediate(rect.gameObject);
        }
        private static void MakeBoot(GlobalManager root)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                var boot = new GameObject("Architecture Bootstrap").AddComponent<ArchitectureBootstrap>();
                var canvas = Rect("Recovery Canvas", boot.transform); Stretch(canvas);
                canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.GetComponent<Canvas>().sortingOrder = 1000;
                var scaler = canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
                canvas.gameObject.AddComponent<GraphicRaycaster>();
                var recovery = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/StartupRecovery.prefab"), canvas);
                Stretch((RectTransform)recovery.transform);
                boot.Configure(root, recovery.GetComponent<StartupRecoveryPanel>()); recovery.SetActive(false);
                // 冷启动失败时也可操作；正常启动后使用持久化根节点的 EventSystem。
                var camera = new GameObject("Bootstrap Camera").AddComponent<Camera>(); camera.enabled = false;
                var light = new GameObject("Bootstrap Light").AddComponent<Light>(); light.type = LightType.Directional; light.enabled = false;
                EditorSceneManager.SaveScene(scene, Boot);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        private static void MakeScene(string id)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            try
            {
                var context = new GameObject("Sample " + id).AddComponent<SampleSceneContext>(); context.StateId = id;
                context.SpawnRoot = new GameObject("Spawn Root").transform; context.SpawnRoot.SetParent(context.transform, false);
                var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
                camera.transform.position = new Vector3(0, 9, -13); camera.transform.LookAt(Vector3.zero); camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = id == "B" ? new Color(.13f, .10f, .22f) : new Color(.06f, .14f, .19f);
                context.SceneCamera = camera;
                var light = new GameObject("Directional Light").AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.5f; light.transform.rotation = Quaternion.Euler(45, -30, 0);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); floor.name = "Sample Ground"; floor.transform.localScale = new Vector3(3, 1, 3);
                string path = Root + "/Scenes/" + id + ".unity"; EditorSceneManager.SaveScene(scene, path); Address(path, "LastLight/Scenes/" + id);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
        private static void Address(string path, string key)
        { Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path), group).SetAddress(key); }
        [MenuItem("Tools/LastLight/Architecture/Validate Configuration")]
        public static void Validate()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PanelCatalog>(Root + "/Configuration/Panels.asset");
            if (catalog == null) throw new InvalidOperationException("请先生成样例。");
            foreach (var definition in catalog.Panels)
            {
                string path = Root + "/Prefabs/" + definition.Id + ".prefab";
                var panel = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (panel == null || panel.GetComponent<PanelBase>() == null) throw new InvalidOperationException("面板缺失: " + path);
                ValidateRect(panel.GetComponent<RectTransform>(), path);
                if (Settings.FindAssetEntry(AssetDatabase.AssetPathToGUID(path))?.address != definition.Address) throw new InvalidOperationException("Addressable 地址错误: " + path);
            }
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/GlobalRoot.prefab");
            for (int i = 0; i < 4; ++i) ValidateRect(root.GetComponent<UIManager>().LayerRoot((UILayer)i), "GlobalRoot/" + (UILayer)i);
            Debug.Log("LastLight panel configuration valid: five full-screen prefabs, four stretched layers.");
        }

        public static string ValidateResolutionMatrix()
        {
            var sizes = new[]
            {
                new Vector2Int(1920, 1080),
                new Vector2Int(1920, 1200),
                new Vector2Int(2560, 1080),
                new Vector2Int(1280, 720)
            };
            var report = new StringBuilder();
            foreach (var size in sizes)
            {
                var viewport = Rect("Viewport", null);
                viewport.sizeDelta = size;
                var layer = Rect("Layer", viewport);
                Stretch(layer);
                try
                {
                    foreach (var definition in AssetDatabase.LoadAssetAtPath<PanelCatalog>(Root + "/Configuration/Panels.asset").Panels)
                    {
                        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + definition.Id + ".prefab");
                        var instance = UnityEngine.Object.Instantiate(prefab, layer, false);
                        try
                        {
                            var panel = (RectTransform)instance.transform;
                            panel.anchoredPosition3D = Vector3.zero;
                            viewport.ForceUpdateRectTransforms();
                            layer.ForceUpdateRectTransforms();
                            panel.ForceUpdateRectTransforms();
                            var panelBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(layer, panel);
                            if (!Approximately(panelBounds.size.x, size.x) || !Approximately(panelBounds.size.y, size.y))
                                throw new InvalidOperationException($"{definition.Id} 未铺满 {size.x}x{size.y}: {panelBounds.size}");
                            var content = panel.Find("Content") as RectTransform;
                            if (content == null) throw new InvalidOperationException(definition.Id + " 缺少 Content 根节点。");
                            var contentBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(layer, content);
                            var screen = layer.rect;
                            if (contentBounds.min.x < screen.xMin - .01f || contentBounds.max.x > screen.xMax + .01f
                                || contentBounds.min.y < screen.yMin - .01f || contentBounds.max.y > screen.yMax + .01f)
                                throw new InvalidOperationException($"{definition.Id}/Content 超出 {size.x}x{size.y}: {contentBounds}");
                        }
                        finally { UnityEngine.Object.DestroyImmediate(instance); }
                    }
                    report.AppendLine($"PASS {size.x}x{size.y}: five panels cover their layer; Content remains inside viewport.");
                }
                finally { UnityEngine.Object.DestroyImmediate(viewport.gameObject); }
            }
            return report.ToString().TrimEnd();
        }

        private static bool Approximately(float actual, float expected) => Mathf.Abs(actual - expected) < .01f;
        private static void ValidateRect(RectTransform rect, string path)
        {
            if (rect == null || rect.anchorMin != Vector2.zero || rect.anchorMax != Vector2.one || rect.pivot != new Vector2(.5f, .5f)
                || rect.offsetMin != Vector2.zero || rect.offsetMax != Vector2.zero || rect.anchoredPosition3D != Vector3.zero
                || rect.localScale != Vector3.one || Quaternion.Angle(rect.localRotation, Quaternion.identity) > .01f)
                throw new InvalidOperationException("面板根节点必须预先全屏拉伸，位置/偏移归零、缩放一、旋转零: " + path);
        }
        [MenuItem("Tools/LastLight/Architecture/Open Sample")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play Mode。");
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(Boot);
        }
        [MenuItem("Tools/LastLight/Architecture/Build Local Content")]
        public static void BuildContent()
        {
            using var protection = new PipelineProtection();
            Validate(); AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error)) throw new InvalidOperationException(result.Error);
        }
        [MenuItem("Tools/LastLight/Architecture/Build Windows Sample")]
        public static void BuildWindows()
        {
            using var protection = new PipelineProtection();
            BuildContent(); Directory.CreateDirectory("Builds/LastLightArchitecture");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Boot }, locationPathName = "Builds/LastLightArchitecture/LastLightArchitecture.exe",
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            Directory.CreateDirectory("Captures/LastLight/Architecture");
            File.WriteAllText("Captures/LastLight/Architecture/BuildResult.txt", report.summary.result + "\nErrors: " + report.summary.totalErrors + "\nSize: " + report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Windows 样例构建失败: " + report.summary.result);
        }
        /// <summary>URP 构建会标脏预筛选字段；完成后恢复构建前的管线资产。</summary>
        private sealed class PipelineProtection : IDisposable
        {
            private readonly Dictionary<UnityEngine.Object, string> snapshots = new Dictionary<UnityEngine.Object, string>();
            public PipelineProtection()
            {
                foreach (var guid in AssetDatabase.FindAssets("t:RenderPipelineAsset", new[] { "Assets" }))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(AssetDatabase.GUIDToAssetPath(guid));
                    if (EditorUtility.IsDirty(asset)) throw new InvalidOperationException("构建前请先保存渲染管线资产: " + AssetDatabase.GetAssetPath(asset));
                    snapshots.Add(asset, EditorJsonUtility.ToJson(asset));
                }
            }
            public void Dispose()
            {
                foreach (var snapshot in snapshots)
                {
                    if (EditorJsonUtility.ToJson(snapshot.Key) == snapshot.Value) continue;
                    EditorJsonUtility.FromJsonOverwrite(snapshot.Value, snapshot.Key);
                    EditorUtility.SetDirty(snapshot.Key); AssetDatabase.SaveAssetIfDirty(snapshot.Key);
                }
            }
        }
    }
}
