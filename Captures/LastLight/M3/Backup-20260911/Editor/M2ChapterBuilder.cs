using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LastLight.Editor
{
    public static partial class M2ChapterBuilder
    {
        public const string Root="Assets/LastLight/M2Generated";
        public const string Boot="Assets/LastLight/Scenes/M2Chapter.unity";
        private static AddressableAssetSettings Settings=>AddressableAssetSettingsDefaultObject.Settings;
        private static AddressableAssetGroup group;
        private static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        private static readonly Dictionary<string,Color> palette=new Dictionary<string,Color>{
            {"Timber",new Color(.25f,.32f,.36f)},{"Edge",new Color(.43f,.49f,.49f)},
            {"Metal",new Color(.13f,.21f,.25f)},{"Rust",new Color(.53f,.29f,.16f)},
            {"Snow",new Color(.70f,.82f,.9f)},{"Warm",new Color(1,.62f,.22f)},
            {"Teal",new Color(.22f,.59f,.57f)},{"Cloth",new Color(.37f,.44f,.42f)},
            {"Dark",new Color(.055f,.09f,.12f)},{"Bone",new Color(.68f,.64f,.48f)},
            {"Bark",new Color(.29f,.25f,.23f)},{"Food",new Color(.51f,.36f,.25f)}};
        [MenuItem("Tools/LastLight/M2/Generate Prototype")]
        public static void Generate()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException(L.K("tcf840296eb"));
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException(L.K("t82bdd9980d"));
            foreach(var folder in new[]{"Prefabs","Scenes","Materials","Configuration","Meshes"})Directory.CreateDirectory(Root+"/"+folder);
            Directory.CreateDirectory("Assets/LastLight/Scenes");AssetDatabase.Refresh();
            if(Settings==null)throw new InvalidOperationException(L.K("td5ff89f44c"));
            Settings.RemoteCatalogBuildPath.SetVariableByName(Settings,AddressableAssetSettings.kLocalBuildPath);
            Settings.RemoteCatalogLoadPath.SetVariableByName(Settings,AddressableAssetSettings.kLocalLoadPath);
            group=Settings.FindGroup("LastLight M2 Local")??Settings.CreateGroup("LastLight M2 Local",false,false,true,null,typeof(BundledAssetGroupSchema),typeof(ContentUpdateGroupSchema));
            var schema=group.GetSchema<BundledAssetGroupSchema>();schema.BuildPath.SetVariableByName(Settings,AddressableAssetSettings.kLocalBuildPath);schema.LoadPath.SetVariableByName(Settings,AddressableAssetSettings.kLocalLoadPath);schema.BundleMode=BundledAssetGroupSchema.BundlePackingMode.PackSeparately;
            var previous=SceneManager.GetActiveScene();var scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scratch);
            try
            {
                materials.Clear();foreach(var pair in palette)Material(pair.Key,pair.Value);
                foreach(Structure kind in Enum.GetValues(typeof(Structure)))MakeStructure(kind);
                MakeRoof();MakeRaider();MakeFootprint();
                var definitions=new[]{Definition("M1HUD",UILayer.BG,false,false,false),Definition("M1Inventory",UILayer.Window,true,true,true),Definition("M1Journal",UILayer.Window,true,true,true),Definition("M1Pause",UILayer.Window,true,true,true),Definition("M1Window",UILayer.Window,true,true,true),Definition("M1Event",UILayer.Pop,true,true,true),Definition("M1End",UILayer.Pop,true,true,true),Definition("M1Build",UILayer.Window,false,true,true),Definition("M1Loading",UILayer.Over,true,true,false)};
                foreach(var definition in definitions)MakePanel(definition);
                var catalog=AssetDatabase.LoadAssetAtPath<PanelCatalog>(Root+"/Configuration/Panels.asset");
                if(catalog==null){catalog=ScriptableObject.CreateInstance<PanelCatalog>();AssetDatabase.CreateAsset(catalog,Root+"/Configuration/Panels.asset");}
                catalog.Panels=definitions;EditorUtility.SetDirty(catalog);var global=MakeGlobal(catalog);
                EditorSceneManager.CloseScene(scratch,true);SceneManager.SetActiveScene(previous);
                MakeScene(true);MakeScene(false);MakeScene(false,true);MakeBoot(global);
                AssetDatabase.SaveAssets();Validate();Debug.Log("M1 prototype generated: "+Boot);
            }
            finally{if(scratch.IsValid()&&scratch.isLoaded)EditorSceneManager.CloseScene(scratch,true);if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);}
        }
        private static Material Material(string name,Color color)
        {
            if(materials.TryGetValue(name,out var existing))return existing;
            string path=Root+"/Materials/"+name+".mat";var value=AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader=Shader.Find("Universal Render Pipeline/Lit");if(shader==null)throw new InvalidOperationException("URP/Lit shader missing");
            if(value==null){value=new Material(shader);AssetDatabase.CreateAsset(value,path);}value.color=color;value.SetFloat("_Smoothness",.06f);
            if(name=="Warm"){value.EnableKeyword("_EMISSION");value.SetColor("_EmissionColor",color*.8f);}
            EditorUtility.SetDirty(value);materials[name]=value;return value;
        }
        private static GameObject Model(string name,Transform parent=null)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>((System.IO.File.Exists("Assets/LastLight/Models/M2/"+name+".fbx")?"Assets/LastLight/Models/M2/":"Assets/LastLight/Models/")+name+".fbx");if(prefab==null)throw new InvalidOperationException(L.K("t75c7c720c2")+name);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.name=name;instance.transform.SetParent(parent,false);
            foreach(var renderer in instance.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>materials.TryGetValue(m.name,out var replacement)?replacement:materials["Metal"]).ToArray();
            return instance;
        }
        private static BoxCollider BoxCollider(GameObject go,Vector3 center,Vector3 size){var box=go.AddComponent<BoxCollider>();box.center=center;box.size=size;return box;}
        private static void MakeStructure(Structure kind)
        {
            var go=new GameObject(kind.ToString());Model(kind.ToString(),go.transform);go.AddComponent<M1Built>();if(kind==Structure.Planter)AddFarmVisual(go);
            if(kind==Structure.Door){BoxCollider(go,new Vector3(-.8f,1.25f,0),new Vector3(.4f,2.5f,.22f));BoxCollider(go,new Vector3(.8f,1.25f,0),new Vector3(.4f,2.5f,.22f));}
            else if(Catalog.Edge(kind))BoxCollider(go,new Vector3(0,1.3f,0),new Vector3(2,2.6f,.22f));
            else if(kind==Structure.Floor)BoxCollider(go,new Vector3(0,.02f,0),new Vector3(2,.14f,2));
            else BoxCollider(go,new Vector3(0,kind==Structure.Barricade?.65f:.55f,0),kind==Structure.Barricade?new Vector3(1.9f,1.3f,.6f):new Vector3(1.55f,1.1f,1.55f));
            if(Catalog.Edge(kind))go.AddComponent<M1Occluder>();SavePrefab(go,kind.ToString());
        }
        private static void MakeRoof(){var go=new GameObject("Roof");Model("Roof",go.transform);go.AddComponent<M1Occluder>();SavePrefab(go,"Roof");}
        private static void MakeRaider(){var go=new GameObject("Raider");Model("Beast",go.transform);go.AddComponent<M1Beast>();var collider=go.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,.6f,0);collider.height=1.2f;collider.radius=.45f;SavePrefab(go,"Raider");}
        private static void MakeFootprint(){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Footprint";Object.DestroyImmediate(go.GetComponent<Collider>());go.transform.localScale=new Vector3(.16f,.006f,.32f);go.GetComponent<Renderer>().sharedMaterial=Material("Footprint",new Color(.43f,.56f,.64f));SavePrefab(go,"Footprint");}
        private static void SavePrefab(GameObject go,string name){string path=Root+"/Prefabs/"+name+".prefab";PrefabUtility.SaveAsPrefabAsset(go,path);Address(path,"LastLight/M2/Prefabs/"+name);Object.DestroyImmediate(go);}
        private static void Address(string path,string key)=>Settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(path),group).SetAddress(key);
        private static PanelDefinition Definition(string id,UILayer layer,bool modal,bool pause,bool closable)=>new PanelDefinition{Id=id,Address="LastLight/M2/UI/"+id,Layer=layer,Modal=modal,Pause=pause,Closable=closable,Lifetime=SystemLifetime.Session};
        private static RectTransform Rect(string name,Transform parent){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
        private static void Stretch(RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=new Vector2(.5f,.5f);rect.offsetMin=rect.offsetMax=Vector2.zero;rect.anchoredPosition3D=Vector3.zero;rect.localScale=Vector3.one;}
        private static void Box(RectTransform rect,Vector2 size,Vector2 position){rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.sizeDelta=size;rect.anchoredPosition=position;}
        private static Image Fill(RectTransform rect,Color color,bool raycast){var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image;}
        private static Text Text(Transform parent,string name,string text,int size,Vector2 dimensions,Vector2 position)
        {
            var rect=Rect(name,parent);Box(rect,dimensions,position);var component=rect.gameObject.AddComponent<Text>();component.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");component.fontSize=size;component.text=text;component.color=new Color(.9f,.94f,.92f);component.alignment=TextAnchor.UpperLeft;component.raycastTarget=false;component.horizontalOverflow=HorizontalWrapMode.Wrap;component.verticalOverflow=VerticalWrapMode.Overflow;return component;
        }
        private static void MakePanel(PanelDefinition definition)
        {
            bool hud=definition.Id=="M1HUD",build=definition.Id=="M1Build",loading=definition.Id=="M1Loading";
            var rect=Rect(definition.Id,null);Stretch(rect);rect.gameObject.AddComponent<CanvasGroup>();
            if(definition.Modal)Fill(rect,new Color(.015f,.027f,.037f,.72f),true);
            var card=Rect("Content",rect);Vector2 dimensions=hud?new Vector2(490,830):build?new Vector2(540,980):new Vector2(1240,980);Box(card,dimensions,Vector2.zero);
            if(hud){card.anchorMin=card.anchorMax=new Vector2(0,1);card.pivot=new Vector2(0,1);card.anchoredPosition=new Vector2(24,-24);}
            if(build){card.anchorMin=card.anchorMax=new Vector2(1,.5f);card.pivot=new Vector2(1,.5f);card.anchoredPosition=new Vector2(-24,0);}
            Fill(card,new Color(.035f,.075f,.09f,hud?.80f:.98f),!hud);
            var title=Text(card,"Title",definition.Id,hud?24:32,new Vector2(dimensions.x-56,50),new Vector2(0,dimensions.y/2-45));title.color=new Color(1,.76f,.39f);
            float bodyHeight=hud?720:build?220:450;
            var viewport=Rect("Viewport",card);Box(viewport,new Vector2(dimensions.x-56,bodyHeight),new Vector2(0,dimensions.y/2-88-bodyHeight/2));Fill(viewport,Color.clear,!hud);viewport.gameObject.AddComponent<RectMask2D>();
            var body=Text(viewport,"Body","",hud?20:24,new Vector2(dimensions.x-64,bodyHeight),Vector2.zero);body.rectTransform.anchorMin=new Vector2(0,1);body.rectTransform.anchorMax=new Vector2(1,1);body.rectTransform.pivot=new Vector2(.5f,1);body.rectTransform.anchoredPosition=Vector2.zero;body.rectTransform.sizeDelta=new Vector2(-8,bodyHeight);
            if(!hud){var fitter=body.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;var scroll=viewport.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=body.rectTransform;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=35;}
            var buttons=new List<Button>();
            if(!hud&&!loading)for(int i=0;i<15;i++)
            {
                int columns=build?2:3;float width=build?235:365;float y=build?100:-105;var buttonRect=Rect("Action "+i,card);Box(buttonRect,new Vector2(width,67),new Vector2((i%columns-(columns-1)*.5f)*(width+16),y-(i/columns)*77));
                Fill(buttonRect,new Color(.12f,.24f,.27f),true);var button=buttonRect.gameObject.AddComponent<Button>();var label=Text(buttonRect,"Label","",21,new Vector2(width-18,63),Vector2.zero);label.alignment=TextAnchor.MiddleCenter;label.resizeTextForBestFit=true;label.resizeTextMinSize=16;label.resizeTextMaxSize=24;buttons.Add(button);
            }
            var panel=rect.gameObject.AddComponent<M1Panel>();panel.Configure(title,body,buttons.ToArray());panel.ConfigureFont(AssetDatabase.LoadAssetAtPath<Font>("Assets/LastLight/Fonts/NotoSansCJKsc-Regular.otf"));string path=Root+"/Prefabs/"+definition.Id+".prefab";PrefabUtility.SaveAsPrefabAsset(rect.gameObject,path);Address(path,definition.Address);Object.DestroyImmediate(rect.gameObject);
        }
        private static GlobalManager MakeGlobal(PanelCatalog catalog)
        {
            var go=new GameObject("LastLight M1 Global");go.AddComponent<AudioListener>();var ui=go.AddComponent<UIManager>();go.AddComponent<GlobalManager>();
            var canvasRect=Rect("Canvas",go.transform);Stretch(canvasRect);canvasRect.gameObject.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;var scaler=canvasRect.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=1;
            var roots=new RectTransform[4];for(int i=0;i<4;i++){roots[i]=Rect(((UILayer)i).ToString(),canvasRect);Stretch(roots[i]);var canvas=roots[i].gameObject.AddComponent<Canvas>();canvas.overrideSorting=true;canvas.sortingOrder=i*100;roots[i].gameObject.AddComponent<GraphicRaycaster>();}ui.Configure(catalog,roots);
            var events=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(go.transform,false);events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var prefab=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/GlobalRoot.prefab");Object.DestroyImmediate(go);return prefab.GetComponent<GlobalManager>();
        }
        private static void MakeBoot(GlobalManager root)
        {
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try{new GameObject("M1 Director").AddComponent<M1Director>().ConfigureM2(root);new GameObject("Bootstrap Camera").AddComponent<Camera>().enabled=false;var light=new GameObject("Bootstrap Light").AddComponent<Light>();light.type=LightType.Directional;light.enabled=false;EditorSceneManager.SaveScene(scene,Boot);}
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        private static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material,bool collide=true,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;if(!collide)Object.DestroyImmediate(go.GetComponent<Collider>());return go;
        }
        private static M1Node Node(List<M1Node> nodes,string id,string model,Vector3 p,NodeKind kind,string label,Resource resource=Resource.Wood,int amount=3,float respawn=180)
        {
            var go=new GameObject(id);go.transform.position=p;Model(model,go.transform);var node=go.AddComponent<M1Node>();node.Configure(kind,label,resource,amount,respawn);nodes.Add(node);
            if(kind!=NodeKind.Pickup){var marker=Cube("Interaction glow",p+Vector3.up*.035f,new Vector3(.5f,.025f,.5f),materials["Warm"],false,go.transform);marker.transform.localRotation=Quaternion.Euler(0,45,0);}
            return node;
        }
        private static void MakeScene(bool home,bool greenhouse=false)
        {
            string id=home?"Home":greenhouse?"Greenhouse":"Workshop";var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.6f,.73f,.85f);RenderSettings.ambientEquatorColor=new Color(.35f,.47f,.57f);RenderSettings.ambientGroundColor=new Color(.24f,.34f,.44f);RenderSettings.fog=false;
                var session=new GameObject("M1 "+id).AddComponent<M1GameSession>();var dynamicRoot=new GameObject("Session objects").transform;
                MakeSnowGround(home);
                var light=new GameObject("Winter sun").AddComponent<Light>();light.type=LightType.Directional;light.transform.rotation=Quaternion.Euler(28,-125,0);light.color=new Color(1,.94f,.84f);light.intensity=1.15f;light.shadows=LightShadows.Soft;
                var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=9;camera.nearClipPlane=.1f;camera.farClipPlane=150;camera.backgroundColor=new Color(.56f,.68f,.77f);var follow=camera.gameObject.AddComponent<M1Camera>();
                var player=new GameObject("Traveler");player.transform.position=home?new Vector3(0,.2f,-14):new Vector3(0,.2f,-32);var motor=player.AddComponent<CharacterController>();motor.center=new Vector3(0,.9f,0);motor.height=1.8f;motor.radius=.28f;motor.stepOffset=.25f;
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SnowVillage/Prefabs/WinterTravelerVisual.prefab"),player.transform);visual.transform.localPosition=Vector3.zero;
                var actor=player.AddComponent<M1Actor>();var animator=visual.GetComponentInChildren<Animator>();actor.Configure(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"),animator,camera);follow.Configure(player.transform);
                camera.transform.position=player.transform.position+new Vector3(15,26,-23);camera.transform.rotation=Quaternion.LookRotation(new Vector3(-15,-26,23));
                var hand=visual.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Hand.R");if(hand!=null){var hammer=Model("Hammer",hand);hammer.transform.localPosition=new Vector3(0,-.03f,.02f);hammer.transform.localRotation=Quaternion.Euler(0,0,90);var inherited=hand.lossyScale;hammer.transform.localScale=new Vector3(1/Mathf.Abs(inherited.x),1/Mathf.Abs(inherited.y),1/Mathf.Abs(inherited.z));}
                var nodes=new List<M1Node>();var beasts=new List<M1Beast>();Transform liang=null;Light stove=null;var gridRoot=new GameObject("Construction grid");
                if(home)
                {
                    Node(nodes,"home-terminal","Terminal",new Vector3(0,0,-10),NodeKind.Terminal,L.K("t4af6f0f2df"));
                    Node(nodes,"emergency-depot","Storage",new Vector3(-3,0,-16),NodeKind.Depot,L.K("ta3d11553b7"));
                    Node(nodes,"emergency-bench","Workbench",new Vector3(3,0,-16),NodeKind.EmergencyBench,L.K("t44a27c4f4c"));
                    var heat=Node(nodes,"emergency-stove","Stove",new Vector3(0,0,-7),NodeKind.EmergencyStove,L.K("t21c0c44838"));stove=new GameObject("Warm light").AddComponent<Light>();stove.transform.SetParent(heat.transform,false);stove.transform.localPosition=new Vector3(0,1.5f,0);stove.type=LightType.Point;stove.color=new Color(1,.55f,.19f);stove.range=10;stove.intensity=4;stove.enabled=false;
                    Node(nodes,"maintenance-pod","Pod",new Vector3(-6,0,-16),NodeKind.Pod,L.K("tf2ac13ec3c"));
                    var worker=Node(nodes,"liang","Liang",new Vector3(-4,0,-14),NodeKind.Liang,L.K("t164353836b"));liang=worker.transform;
                    Node(nodes,"workshop-sign","Terminal",new Vector3(20,0,-10),NodeKind.Travel,L.K("t97a1ccbf21"));Node(nodes,"aid-drain","Scrap",new Vector3(21,0,12),NodeKind.AidRoute,L.K("tcb5ca504cf"));
                    for(int i=0;i<8;i++)Node(nodes,"wood-"+i,"Wood",new Vector3(i<4?-19:19,0,-3+(i%4)*5),NodeKind.Pickup,L.K("tb6b7352c2d"),Resource.Wood,4);
                    for(int i=0;i<3;i++){Node(nodes,"stone-"+i,"Stone",new Vector3(-10+i*5,0,19),NodeKind.Pickup,L.K("t76b0338675"),Resource.Stone,3);Node(nodes,"fiber-"+i,"Fiber",new Vector3(-19,0,-13-i*3),NodeKind.Pickup,L.K("td18ebe4d87"),Resource.Fiber,3);Node(nodes,"food-"+i,"Ration",new Vector3(10+i*3,0,-20),NodeKind.Pickup,L.K("t0687244370"),Resource.Ration,2);Node(nodes,"scrap-"+i,"Scrap",new Vector3(20,0,-20+i*3),NodeKind.Pickup,L.K("t6f1a720751"),Resource.Scrap,3);}
                    var line=Material("Grid",new Color(.52f,.63f,.65f));for(int i=-7;i<=8;i++){Cube("Build grid",new Vector3(i*2-1,.002f,0),new Vector3(.025f,.012f,30),line,false,gridRoot.transform);Cube("Build grid",new Vector3(0,.002f,i*2-1),new Vector3(30,.012f,.025f),line,false,gridRoot.transform);}
                    Cube("Protected rescue lane",new Vector3(0,.006f,0),new Vector3(.08f,.01f,30),materials["Warm"],false,gridRoot.transform);
                    var shelter=new GameObject("Old maintenance shelter").transform;
                    for(int x=-7;x<=7;x+=2){if(Mathf.Abs(x)<2)continue;var wall=Model("Wall",shelter);wall.transform.localPosition=new Vector3(x,0,-20);BoxCollider(wall,new Vector3(0,1.3f,0),new Vector3(2,2.6f,.22f));}
                    foreach(float x in new[]{-8f,8f}){Cube("Canopy post",new Vector3(x,1.6f,-16),new Vector3(.18f,3.2f,.18f),materials["Timber"],true,shelter);Cube("Canopy beam",new Vector3(x,3.1f,-18),new Vector3(.2f,.2f,4.2f),materials["Edge"],false,shelter);}
                    Cube("Snow on back beam",new Vector3(0,2.85f,-20),new Vector3(16.5f,.16f,.65f),materials["Snow"],false,shelter);
                    Cube("Maintenance deck",new Vector3(0,-.015f,-17),new Vector3(16,.08f,5),materials["Timber"],true,shelter);
                    for(int i=0;i<9;i++)Cube("Worn snow path",new Vector3(5+i*1.7f,.003f,-12-i*.05f),new Vector3(1.15f,.015f,.7f),Material("PackedSnow",new Color(.6f,.73f,.81f)),false);
                    for(int i=0;i<8;i++){float x=-27+i*7;Cube("Old fence",new Vector3(x,.5f,25),new Vector3(5,1,.18f),materials["Timber"]);}
                }
                else if(greenhouse)
                {
                    MakeGreenhouse(nodes);player.transform.position=new Vector3(0,.2f,-12);
                }
                else
                {
                    Node(nodes,"return-road","Terminal",new Vector3(0,0,-35),NodeKind.Travel,L.K("t1ed8f79cde"));
                    Node(nodes,"workshop-core","Core",new Vector3(7,0,47),NodeKind.Core,L.K("t5bac6ec81e"));
                    Node(nodes,"departure-record","Terminal",new Vector3(-8,0,22),NodeKind.Record,L.K("tc509c3b92c"));
                    Node(nodes,"return-shortcut","Terminal",new Vector3(12,0,45),NodeKind.Shortcut,L.K("t07fed7017e"));
                    for(int i=0;i<8;i++)Node(nodes,"salvage-"+i,i%3==0?"Wood":"Scrap",new Vector3(i%2==0?-15:14,0,-17+i*8),NodeKind.Pickup,i%3==0?L.K("t5645165ee4"):L.K("t7beb3ada44"),i%3==0?Resource.Wood:Resource.Scrap,4);
                    for(int i=0;i<9;i++){var ruin=new GameObject("Workshop wall remnant").transform;ruin.position=new Vector3(i%2==0?-6:6,0,-8+i*6);ruin.rotation=Quaternion.Euler(0,i*19,0);for(int segment=0;segment<2;segment++){var panel=Model(i%3==0?"Window":"Wall",ruin);panel.transform.localPosition=new Vector3(segment*2,0,0);BoxCollider(panel,new Vector3(0,1.25f,0),new Vector3(2,2.5f,.25f));}var table=Model("Workbench",ruin);table.transform.localPosition=new Vector3(0,0,1.3f);}
                    var enemy=new GameObject("Workshop beast");enemy.transform.position=new Vector3(2,0,12);Model("Beast",enemy.transform);var beast=enemy.AddComponent<M1Beast>();var collider=enemy.AddComponent<CapsuleCollider>();collider.radius=.45f;collider.height=1.2f;collider.center=new Vector3(0,.6f,0);beasts.Add(beast);
                    for(int i=0;i<14;i++)Cube("Path marker",new Vector3(-18,.025f,-27+i*5),new Vector3(.25f,.05f,.8f),materials["Warm"],false);
                    Cube("Old workshop platform",new Vector3(6,0,47),new Vector3(14,.15f,8),materials["Timber"]);
                }
                // A bounded winter environment, outside the usable routes and construction reserve.
                var rng=new System.Random(home?71:91);for(int i=0;i<38;i++){float x=(i%2==0?-1:1)*(26+(float)rng.NextDouble()*12);float z=(float)rng.NextDouble()*(home?70:115)-(home?35:40);var trunk=Cube("Bare tree",new Vector3(x,2.3f,z),new Vector3(.35f,4.6f,.35f),materials["Bark"]);for(int branch=0;branch<3;branch++){var limb=Cube("Branch",new Vector3(x+(branch-1)*.55f,2.5f+branch*.55f,z),new Vector3(.17f,2.2f,.17f),materials["Bark"],false,trunk.transform);limb.transform.rotation=Quaternion.Euler(0,branch*65,branch%2==0?40:-45);}}
                var navigationRoot=new GameObject("Navigation static environment");
                foreach(var root in scene.GetRootGameObjects())
                    if(root!=navigationRoot && root!=session.gameObject && root!=dynamicRoot.gameObject && root!=player && root!=camera.gameObject && root!=gridRoot && (liang==null||root!=liang.gameObject) && !beasts.Any(b=>b.gameObject==root))
                        root.transform.SetParent(navigationRoot.transform,true);
                var surface=navigationRoot.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;
                surface.overrideVoxelSize=true;surface.voxelSize=.1f;
                session.ConfigureNavigation(surface);
                AddM2Environment(scene,home,greenhouse,nodes);
                gridRoot.SetActive(false);session.Configure(home,actor,follow,camera,materials["Warm"],dynamicRoot,liang,stove,nodes.ToArray(),beasts.ToArray(),gridRoot);session.ValidateConfiguration();
                string path=Root+"/Scenes/"+id+".unity";EditorSceneManager.SaveScene(scene,path);Address(path,"LastLight/M2/Scenes/"+id);
            }
            finally{EditorSceneManager.CloseScene(scene,true);}
        }
        [MenuItem("Tools/LastLight/M2/Open Prototype")]
        public static void Open(){if(!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;EditorSceneManager.OpenScene(Boot);}
        [MenuItem("Tools/LastLight/M2/Validate Configuration")]
        public static void Validate()
        {
            var catalog=AssetDatabase.LoadAssetAtPath<PanelCatalog>(Root+"/Configuration/Panels.asset");if(catalog==null)throw new InvalidOperationException(L.K("tec726ad2c7"));
            foreach(var panel in catalog.Panels){var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+panel.Id+".prefab");if(prefab==null||prefab.GetComponent<M1Panel>()==null)throw new InvalidOperationException(L.K("te4965bd3e2")+panel.Id);var rect=prefab.GetComponent<RectTransform>();if(rect.anchorMin!=Vector2.zero||rect.anchorMax!=Vector2.one||rect.offsetMin!=Vector2.zero||rect.offsetMax!=Vector2.zero)throw new InvalidOperationException(L.K("tdaa3085098")+panel.Id);}
            Debug.Log(L.K("t1fcb26bac1"));
        }
        private static void MakeSnowGround(bool home)
        {
            int nx=90,nz=home?90:130;var vertices=new Vector3[(nx+1)*(nz+1)];var triangles=new int[nx*nz*6];int index=0;
            for(int z=0;z<=nz;z++)for(int x=0;x<=nx;x++){float wx=x-nx*.5f,wz=z-nz*.5f+(home?0:12);float blend=Mathf.Clamp01((Mathf.Max(Mathf.Abs(wx),Mathf.Abs(wz))-16)/8);float height=(Mathf.PerlinNoise(wx*.075f+10,wz*.075f+10)-.5f)*.65f*blend;vertices[z*(nx+1)+x]=new Vector3(wx,height-.025f,wz);}
            for(int z=0;z<nz;z++)for(int x=0;x<nx;x++){int p=z*(nx+1)+x;triangles[index++]=p;triangles[index++]=p+nx+1;triangles[index++]=p+1;triangles[index++]=p+1;triangles[index++]=p+nx+1;triangles[index++]=p+nx+2;}
            string path=Root+"/Meshes/"+(home?"HomeSnow":"WorkshopSnow")+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh==null){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}mesh.Clear();mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var go=new GameObject("Winter snow ground",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=materials["Snow"];go.GetComponent<MeshCollider>().sharedMesh=mesh;
        }
    }
}
