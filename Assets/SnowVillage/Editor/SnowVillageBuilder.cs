using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

namespace SnowVillage.Editor
{
    public static class SnowVillageBuilder
    {
        private const string Root = "Assets/SnowVillage";
        public const string ScenePath = Root + "/Scenes/SnowVillage.unity";
        private static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            {"Snow",new Color(.67f,.80f,.96f)}, {"OldTimber",new Color(.27f,.34f,.41f)},
            {"TimberEdges",new Color(.36f,.44f,.51f)}, {"DarkWood",new Color(.18f,.24f,.31f)},
            {"RoofMetal",new Color(.26f,.34f,.43f)}, {"WarmWindow",new Color(.30f,.20f,.135f)},
            {"Iron",new Color(.17f,.24f,.31f)}, {"Parka",new Color(.29f,.38f,.44f)},
            {"ParkaSeams",new Color(.20f,.28f,.33f)}, {"Trousers",new Color(.22f,.29f,.35f)},
            {"BootLeather",new Color(.15f,.20f,.25f)}, {"BootSoles",new Color(.10f,.14f,.18f)},
            {"Face",new Color(.72f,.49f,.37f)}, {"RustScarf",new Color(.85f,.28f,.12f)},
            {"WoolHat",new Color(.26f,.36f,.43f)}, {"HatRib",new Color(.40f,.48f,.53f)},
            {"Eyes",new Color(.07f,.09f,.11f)}
        };
        private static Material Mat(string name)
        {
            string path=Root+"/Materials/"+name+".mat";
            Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(m==null) { m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path); }
            m.SetColor("_BaseColor",Colors[name]); m.SetFloat("_Smoothness",.02f); m.SetFloat("_Metallic",0); EditorUtility.SetDirty(m); return m;
        }
        private static void Folders()
        { foreach(string f in new[]{"Materials","Meshes","Prefabs","Animation","Scenes"}) Directory.CreateDirectory(Root+"/"+f); AssetDatabase.Refresh(); }
        private static void MapMaterials(GameObject g)
        {
            foreach(var r in g.GetComponentsInChildren<Renderer>(true))
            {
                var materials=r.sharedMaterials;
                for(int i=0;i<materials.Length;i++) if(materials[i]!=null && Colors.ContainsKey(materials[i].name)) materials[i]=Mat(materials[i].name);
                r.sharedMaterials=materials;
            }
        }
        private static AnimationClip ImportCharacter(string clipName)
        {
            string path=Root+"/Models/Traveler_"+clipName+".fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(path);
            importer.animationType=ModelImporterAnimationType.Generic; importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation=true; importer.animationCompression=ModelImporterAnimationCompression.Off;
            var clips=importer.defaultClipAnimations;
            foreach(var c in clips)
            {
                c.name=clipName; c.loopTime=true; c.loopPose=true; c.lockRootPositionXZ=true; c.lockRootHeightY=true; c.lockRootRotation=true;
                if(clipName=="Walk") c.events=new[]{new AnimationEvent{functionName="Footstep",time=.02f,intParameter=0},new AnimationEvent{functionName="Footstep",time=.52f,intParameter=1}};
            }
            importer.clipAnimations=clips; importer.SaveAndReimport();
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(a=>!a.name.StartsWith("__preview__"));
        }
        [MenuItem("Tools/Snow Village/1 Build Character")]
        public static void BuildCharacter()
        {
            Folders(); var idle=ImportCharacter("Idle"); var walk=ImportCharacter("Walk");
            string path=Root+"/Animation/Traveler.controller";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(controller==null) controller=AnimatorController.CreateAnimatorControllerAtPath(path);
            foreach(var layer in controller.layers) foreach(var st in layer.stateMachine.states) layer.stateMachine.RemoveState(st.state);
            controller.parameters=new[]{new AnimatorControllerParameter{name="Speed",type=AnimatorControllerParameterType.Float}};
            var state=controller.layers[0].stateMachine.AddState("Locomotion");
            var blend=AssetDatabase.LoadAssetAtPath<BlendTree>(Root+"/Animation/Locomotion.asset");
            if(blend==null){blend=new BlendTree();AssetDatabase.CreateAsset(blend,Root+"/Animation/Locomotion.asset");}
            blend.name="Locomotion"; blend.blendType=BlendTreeType.Simple1D; blend.blendParameter="Speed"; blend.useAutomaticThresholds=false;
            blend.children=new[]{new ChildMotion{motion=idle,threshold=0,timeScale=1},new ChildMotion{motion=walk,threshold=1.05f,timeScale=1}};
            state.motion=blend; controller.layers[0].stateMachine.defaultState=state;
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/Traveler_Idle.fbx"));
            g.name="WinterTravelerVisual"; MapMaterials(g);
            var animator=g.GetComponent<Animator>(); if(animator==null) animator=g.AddComponent<Animator>();
            animator.runtimeAnimatorController=controller; animator.applyRootMotion=false; animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            g.AddComponent<SnowFootstepRelay>();
            PrefabUtility.SaveAsPrefabAsset(g,Root+"/Prefabs/WinterTravelerVisual.prefab");
            var bounds=g.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
            UnityEngine.Object.DestroyImmediate(g); EditorUtility.SetDirty(controller); EditorUtility.SetDirty(blend); AssetDatabase.SaveAssets();
            Debug.Log("Snow character imported: "+bounds+"; Idle="+idle.length+"s Walk="+walk.length+"s");
        }
        private static GameObject Prop(string name, Vector3 position, float rotation, Transform parent)
        {
            string prefabPath=Root+"/Prefabs/"+name+".prefab";
            var wrapper=new GameObject(name);
            var original=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Models/"+name+".fbx"));
            original.transform.SetParent(wrapper.transform,false);
            MapMaterials(original); PrefabUtility.SaveAsPrefabAsset(wrapper,prefabPath); UnityEngine.Object.DestroyImmediate(wrapper);
            var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            g.transform.SetParent(parent); g.transform.SetPositionAndRotation(position,Quaternion.Euler(0,rotation,0)); return g;
        }
        private static float Height(float x,float z)
        {
            float h=.06f+.05f*Mathf.PerlinNoise(x*.19f+80,z*.19f+80);
            h+=1.8f*Mathf.Exp(-((x+10)*(x+10)/23+(z+2)*(z+2)/14));
            h+=1.1f*Mathf.Exp(-((x-10)*(x-10)/25+(z-9)*(z-9)/18));
            h+=.85f*Mathf.Exp(-((x+15)*(x+15)/28+(z-1)*(z-1)/5));
            h+=.65f*Mathf.Exp(-((x+19)*(x+19)/18+(z-13)*(z-13)/30));
            h+=.55f*Mathf.Exp(-((x-2)*(x-2)/22+(z+11)*(z+11)/15));
            return h;
        }
        private static Mesh SaveMesh(Mesh mesh,string name)
        {
            string path=Root+"/Meshes/"+name+".asset"; mesh.name=name;
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(old==null) AssetDatabase.CreateAsset(mesh,path); else {EditorUtility.CopySerialized(mesh,old); UnityEngine.Object.DestroyImmediate(mesh); mesh=old;}
            return mesh;
        }
        private static Mesh GroundMesh()
        {
            const int n=160; var vertices=new Vector3[(n+1)*(n+1)]; var uv=new Vector2[vertices.Length]; var triangles=new int[n*n*6];
            for(int z=0;z<=n;z++) for(int x=0;x<=n;x++) {int i=z*(n+1)+x; float px=(x/(float)n-.5f)*70; float pz=(z/(float)n-.5f)*65; vertices[i]=new Vector3(px,Height(px,pz),pz); uv[i]=new Vector2(x/(float)n,z/(float)n);}
            int t=0;
            for(int z=0;z<n;z++)for(int x=0;x<n;x++){int i=z*(n+1)+x; triangles[t++]=i;triangles[t++]=i+n+1;triangles[t++]=i+1;triangles[t++]=i+1;triangles[t++]=i+n+1;triangles[t++]=i+n+2;}
            var mesh=new Mesh {vertices=vertices,triangles=triangles,uv=uv};mesh.RecalculateNormals();var normals=mesh.normals;for(int i=0;i<normals.Length;i++)normals[i]=new Vector3(normals[i].x*2.2f,normals[i].y,normals[i].z*2.2f).normalized;mesh.normals=normals;mesh.RecalculateBounds(); return SaveMesh(mesh,"SnowTerrain");
        }
        private static Mesh PrintMesh()
        {
            const int n=12; var v=new List<Vector3>();var c=new List<Color>();var tris=new List<int>();
            v.Add(new Vector3(0,.006f,0));c.Add(new Color(.34f,.45f,.57f,1));
            for(int r=0;r<3;r++) for(int i=0;i<n;i++)
            {
                float a=i*Mathf.PI*2/n;float radius=r==0?.62f:r==1?.9f:1.12f;
                v.Add(new Vector3(Mathf.Cos(a)*.095f*radius,r==0?.009f:r==1?.04f:.013f,Mathf.Sin(a)*.165f*radius));
                c.Add(r==0?new Color(.43f,.55f,.68f):r==1?new Color(.73f,.85f,.99f):new Color(.60f,.74f,.91f));
            }
            for(int i=0;i<n;i++){tris.Add(0);tris.Add(1+(i+1)%n);tris.Add(1+i);}
            for(int r=0;r<2;r++)for(int i=0;i<n;i++){int a=1+r*n+i,b=1+r*n+(i+1)%n,d=a+n,e=b+n;tris.Add(a);tris.Add(b);tris.Add(d);tris.Add(b);tris.Add(e);tris.Add(d);}
            var mesh=new Mesh();mesh.SetVertices(v);mesh.SetColors(c.Select(color=>color.linear).ToList());mesh.SetTriangles(tris,0);mesh.RecalculateNormals(); return SaveMesh(mesh,"SnowFootprint");
        }
        private static void BoxCollider(GameObject g,Vector3 center,Vector3 size)
        {var c=g.AddComponent<BoxCollider>();c.center=center;c.size=size;}
        private static void Cable(Vector3 a,Vector3 b,Transform parent)
        {
            var g=new GameObject("Overhead power cable");g.transform.SetParent(parent);var line=g.AddComponent<LineRenderer>();line.sharedMaterial=Mat("Iron");line.positionCount=33;line.widthMultiplier=.038f;line.numCornerVertices=1;
            for(int i=0;i<33;i++){float t=i/32f;line.SetPosition(i,Vector3.Lerp(a,b,t)+Vector3.down*(Mathf.Sin(t*Mathf.PI)*.35f));}
        }
        private static ParticleSystem Particles(string name,Transform parent,bool snowfall)
        {
            var g=new GameObject(name);g.transform.SetParent(parent);var ps=g.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.loop=snowfall;main.playOnAwake=snowfall;main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=snowfall?450:180;
            main.startLifetime=snowfall?14:.6f; main.startSpeed=snowfall?0:.35f;main.startSize=snowfall?new ParticleSystem.MinMaxCurve(.025f,.075f):new ParticleSystem.MinMaxCurve(.025f,.09f);main.startColor=new Color(.85f,.92f,1,.8f);main.gravityModifier=snowfall?0:.10f;
            var emission=ps.emission;emission.rateOverTime=snowfall?24:0;
            var shape=ps.shape;shape.shapeType=snowfall?ParticleSystemShapeType.Box:ParticleSystemShapeType.Hemisphere;shape.scale=snowfall?new Vector3(48,1,40):Vector3.one*.13f;shape.radius=.1f;
            var velocity=ps.velocityOverLifetime;velocity.enabled=snowfall;velocity.x=.12f;velocity.y=-.65f;velocity.z=.10f;
            var size=ps.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,1,1,0));
            string p=Root+"/Materials/Snowflakes.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(p);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(material,p);}
            material.SetColor("_BaseColor",new Color(.86f,.93f,1,1));material.SetFloat("_Surface",1);material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);material.SetFloat("_ZWrite",0);material.renderQueue=3000;material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.renderMode=ParticleSystemRenderMode.Billboard;
            if(snowfall){g.transform.position=new Vector3(0,9,0);ps.Play();}return ps;
        }
        [MenuItem("Tools/Snow Village/2 Build Scene")]
        public static void BuildScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before rebuilding.");
            Folders();
            var current=EditorSceneManager.GetActiveScene();
            if(current.isDirty) throw new InvalidOperationException("Save current scene edits before rebuilding.");
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Materials/SnowPipeline.asset");
            if(pipeline==null){AssetDatabase.CopyAsset("Assets/Settings/PC_RPAsset.asset",Root+"/Materials/SnowPipeline.asset");pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(Root+"/Materials/SnowPipeline.asset");}
            var pipelineData=new SerializedObject(pipeline);pipelineData.FindProperty("m_ShadowDistance").floatValue=110;pipelineData.FindProperty("m_MainLightShadowmapResolution").intValue=4096;pipelineData.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.renderPipeline=pipeline;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var environment=new GameObject("Snow Village Environment");
            var ground=new GameObject("SnowGround");ground.transform.SetParent(environment.transform);
            var mesh=GroundMesh();ground.AddComponent<MeshFilter>().sharedMesh=mesh;ground.AddComponent<MeshRenderer>().sharedMaterial=Mat("Snow");ground.AddComponent<MeshCollider>().sharedMesh=mesh;
            var shed=Prop("Woodshed",new Vector3(3,Height(3,1)-.06f,1),-8,environment.transform);shed.transform.localScale=Vector3.one*1.15f;BoxCollider(shed,new Vector3(0,1,0),new Vector3(3,2,2));
            var cabin=Prop("Cabin",new Vector3(1,Height(1,18)-.10f,18),162,environment.transform);BoxCollider(cabin,new Vector3(0,2,0),new Vector3(6,4,4.8f));
            var well=Prop("CoveredWell",new Vector3(-17,Height(-17,10),10),-35,environment.transform);BoxCollider(well,new Vector3(0,.7f,0),new Vector3(1.6f,1.4f,1.6f));
            for(int i=0;i<7;i++){float x=-24+i*3;float z=-11-(x+24)*.30f;var fence=Prop("FenceSection",new Vector3(x,Height(x,z),z),17,environment.transform);BoxCollider(fence,new Vector3(0,.6f,0),new Vector3(3.2f,1.2f,.2f));}
            Vector3 polePos=new Vector3(16,Height(16,-13),-13); var pole=Prop("UtilityPole",polePos,0,environment.transform);pole.transform.localScale=new Vector3(1,1.5f,1);BoxCollider(pole,new Vector3(0,3.8f,0),new Vector3(.3f,7.6f,.3f));
            var backPole=Prop("UtilityPole",new Vector3(-1,Height(-1,22),22),0,environment.transform);
            Cable(polePos+new Vector3(.48f,11.22f,0),backPole.transform.position+new Vector3(.48f,7.48f,0),environment.transform);
            for(int i=0;i<3;i++)Prop("BareTree",new Vector3(17+i*4,Height(17+i*4,18),18+i*3),i*73,environment.transform);
            // Invisible perimeter retains the fixed reference composition during manual movement.
            foreach(var pair in new[]{new Vector4(-23,0,.3f,36),new Vector4(23,0,.3f,36),new Vector4(0,-13,46,.3f),new Vector4(0,19,46,.3f)})
            {var wall=new GameObject("Scene boundary");wall.transform.SetParent(environment.transform);wall.transform.position=new Vector3(pair.x,1,pair.y);BoxCollider(wall,Vector3.zero,new Vector3(pair.z,4,pair.w));}
            var cameraGo=new GameObject("Main Camera");cameraGo.tag="MainCamera";var camera=cameraGo.AddComponent<Camera>();cameraGo.AddComponent<AudioListener>();cameraGo.AddComponent<UniversalAdditionalCameraData>();
            camera.transform.position=new Vector3(0,32,-32);camera.transform.LookAt(new Vector3(0,0,2));camera.orthographic=true;camera.orthographicSize=13;camera.nearClipPlane=.1f;camera.farClipPlane=150;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.60f,.74f,.9f);camera.allowHDR=true;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var lightGo=new GameObject("Winter sun");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(1,.94f,.86f);light.intensity=1.05f;light.shadows=LightShadows.Soft;light.shadowStrength=.78f;light.shadowBias=.035f;light.shadowNormalBias=.12f;lightGo.transform.rotation=Quaternion.Euler(27,-125,0);
            RenderSettings.sun=light;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.62f,.75f,.94f);RenderSettings.ambientEquatorColor=new Color(.40f,.55f,.73f);RenderSettings.ambientGroundColor=new Color(.31f,.43f,.62f);RenderSettings.fog=false;
            var volumeGo=new GameObject("Snow color grading");var volume=volumeGo.AddComponent<Volume>();volume.isGlobal=true;
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(Root+"/Materials/SnowGrade.asset");if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,Root+"/Materials/SnowGrade.asset");}
            if(!profile.TryGet<ColorAdjustments>(out var grade))grade=profile.Add<ColorAdjustments>(true);grade.postExposure.Override(.6f);grade.saturation.Override(-2);grade.contrast.Override(5);
            if(!profile.TryGet<Tonemapping>(out var tone))tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.Neutral);volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
            var effects=new GameObject("Snow and footsteps");var powder=Particles("Footfall snow powder",effects.transform,false);Particles("Falling snow",effects.transform,true);
            var footprint=PrintMesh();string printPath=Root+"/Materials/Footprint.mat";var printMat=AssetDatabase.LoadAssetAtPath<Material>(printPath);if(printMat==null){printMat=new Material(Shader.Find("SnowVillage/Footprint"));AssetDatabase.CreateAsset(printMat,printPath);}
            var prints=effects.AddComponent<SnowFootprints>();prints.Configure(footprint,printMat,powder);
            var trail=new GameObject("Earlier tracks in the snow");trail.transform.SetParent(environment.transform);
            Physics.SyncTransforms();
            for(int i=0;i<54;i++)
            {
                float z=-13+i*.58f;float x=5.7f+1.5f*Mathf.Sin(z*.17f);float side=i%2==0?-.15f:.15f;
                var p=new GameObject("Old footprint");p.transform.SetParent(trail.transform);p.transform.position=new Vector3(x+side,Height(x+side,z)+.025f,z);Vector3 direction=new Vector3(.255f*Mathf.Cos(z*.17f),0,1);
                if(Physics.Raycast(p.transform.position+Vector3.up*3,Vector3.down,out var hit,5)){p.transform.position=hit.point+hit.normal*.025f;p.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(direction,hit.normal),hit.normal);}
                p.AddComponent<MeshFilter>().sharedMesh=footprint;p.AddComponent<MeshRenderer>().sharedMaterial=printMat;
            }
            var player=new GameObject("Winter Traveler");player.transform.position=new Vector3(0,Height(0,1)+.05f,1);player.transform.rotation=Quaternion.Euler(0,35,0);
            var motor=player.AddComponent<CharacterController>();motor.height=1.78f;motor.radius=.24f;motor.center=new Vector3(0,.90f,0);motor.stepOffset=.22f;motor.slopeLimit=45;motor.skinWidth=.025f;
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/WinterTravelerVisual.prefab"));visual.transform.SetParent(player.transform,false);
            var traveler=player.AddComponent<SnowTraveler>();var animator=visual.GetComponent<Animator>();
            var route=new[]{new Vector3(-1,0,4),new Vector3(0,0,8),new Vector3(5.5f,0,10),new Vector3(8,0,6),new Vector3(7,0,-3),new Vector3(1,0,-5),new Vector3(-2,0,-1)};
            traveler.Configure(animator,camera,AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"),prints,route);
            Transform left=visual.GetComponentsInChildren<Transform>().First(t=>t.name=="Foot.L"),right=visual.GetComponentsInChildren<Transform>().First(t=>t.name=="Foot.R");
            visual.GetComponent<SnowFootstepRelay>().Configure(traveler,left,right);
            var effectsPrefab=PrefabUtility.SaveAsPrefabAsset(effects,Root+"/Prefabs/SnowEffects.prefab");
            traveler.Configure(animator,null,AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"),effectsPrefab.GetComponent<SnowFootprints>(),route);
            PrefabUtility.SaveAsPrefabAsset(player,Root+"/Prefabs/WinterTraveler.prefab");
            traveler.Configure(animator,camera,AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions"),prints,route);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Selection.activeGameObject=player;
            if(SceneView.lastActiveSceneView!=null)SceneView.lastActiveSceneView.LookAt(new Vector3(0,0,2),camera.transform.rotation,26);
            Debug.Log("SnowVillage built: "+ScenePath);
        }
        [MenuItem("Tools/Snow Village/3 Capture Scene")]
        public static void CaptureScene() { Capture("Overview",false); }
        public static string Capture(string name,bool closeup)
        {
            var camera=Camera.main;var position=camera.transform.position;var rotation=camera.transform.rotation;float ortho=camera.orthographicSize;
            if(closeup){var player=UnityEngine.Object.FindFirstObjectByType<SnowTraveler>();camera.transform.position=player.transform.position+new Vector3(3,2.1f,4);camera.transform.LookAt(player.transform.position+Vector3.up*.85f);camera.orthographicSize=1.35f;}
            var rt=new RenderTexture(closeup?1100:1900,closeup?1100:1000,24);var old=camera.targetTexture;var active=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var tex=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);tex.Apply();Directory.CreateDirectory("Captures/SnowVillage");string path="Captures/SnowVillage/"+name+".png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);return Path.GetFullPath(path);}
            finally{camera.targetTexture=old;RenderTexture.active=active;rt.Release();UnityEngine.Object.DestroyImmediate(rt);camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=ortho;}
        }
    }
}
