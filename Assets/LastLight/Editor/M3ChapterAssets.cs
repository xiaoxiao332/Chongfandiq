using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight.Editor
{
    public static partial class M3ChapterBuilder
    {
        private static void AddFarmVisual(GameObject go)
        {
            var visual=go.AddComponent<M2FarmVisual>();visual.cover=Model("Protection",go.transform);visual.stages=new GameObject[9];
            for(int crop=0;crop<3;crop++)for(int stage=0;stage<3;stage++)visual.stages[crop*3+stage]=Model("Crop"+crop+"Stage"+stage,go.transform);
            visual.Apply(new Building(1,Structure.Planter,0,0,0));
        }
        private static void MakeGreenhouse(List<M1Node> nodes)
        {
            var greenhouse=Model("Greenhouse");greenhouse.transform.position=new Vector3(0,0,3);
            Node(nodes,"greenhouse-return","Terminal",new Vector3(0,0,-15),NodeKind.Travel,L.K("td218177a62"));
            for(int i=0;i<3;i++){var node=Node(nodes,"sample-"+i,"Crop"+i+"Stage2",new Vector3(-4+i*4,0,3),NodeKind.Sample,L.K("td7071a46f8")+Catalog.ResourceNames[(int)M2WorldSystem.Crops[i]],Resource.Wood,i);}
            Node(nodes,"greenhouse-record","Terminal",new Vector3(-4,0,9),NodeKind.GreenhouseRecord,L.K("t138716a1c2"));
            for(int i=0;i<3;i++)Node(nodes,"greenhouse-food-"+i,"Ration",new Vector3(6,0,-3+i*3),NodeKind.Pickup,L.K("t0687244370"),Resource.Ration,2);
            Cube("Greenhouse left wall",new Vector3(-7,1.5f,3),new Vector3(.2f,3,18),materials["Metal"]);
            Cube("Greenhouse right wall",new Vector3(7,1.5f,3),new Vector3(.2f,3,18),materials["Metal"]);
            Cube("Greenhouse rear wall",new Vector3(0,1.5f,12),new Vector3(14,3,.2f),materials["Metal"]);
        }
        private static void AddM2Environment(Scene scene,bool home,bool greenhouse,List<M1Node> nodes)
        {
            var go=new GameObject("M2 seasonal environment");var visual=go.AddComponent<M2SeasonVisual>();
            foreach(var root in scene.GetRootGameObjects())foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())if(renderer.name=="Winter snow ground")visual.ground=renderer;
            visual.springGround=Material("SpringGround",new Color(.39f,.48f,.39f));
            visual.spring=new GameObject("Spring plants");visual.spring.transform.SetParent(go.transform,false);
            for(int i=0;i<18;i++){var plant=Model("Crop1Stage1",visual.spring.transform);plant.transform.position=new Vector3(i%2==0?-23:23,0,-18+i*2.5f);plant.transform.localScale=Vector3.one*.5f;}
            if(home)
            {
                visual.brokenBridge=Model("BridgeBroken",go.transform);visual.repairedBridge=Model("Bridge",go.transform);
                visual.brokenBridge.transform.position=visual.repairedBridge.transform.position=new Vector3(-23,0,-8);
                Node(nodes,"bridge-work","Terminal",new Vector3(-20,0,-8),NodeKind.Bridge,L.K("tc1d968eeb9"));
                Node(nodes,"greenhouse-sign","Terminal",new Vector3(-27,0,-8),NodeKind.Greenhouse,L.K("t03b721c145"));
            }
            visual.Apply(false,false);
        }
        [MenuItem("Tools/LastLight/M3/Build Windows")]
        public static void BuildWindows()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before building");
            Validate();EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(Boot,true)};
            Directory.CreateDirectory("Builds/LastLight/M3");Directory.CreateDirectory("Captures/LastLight/M3");
            var previousAddressablesMode=Settings.BuildAddressablesWithPlayerBuild;
            const string protectedPipeline="Assets/SnowVillage/Materials/SnowPipeline.asset";
            var protectedPipelineBytes=File.ReadAllBytes(protectedPipeline);
            try
            {
                Settings.RemoteCatalogBuildPath.SetVariableByName(Settings,AddressableAssetSettings.kLocalBuildPath);
                Settings.RemoteCatalogLoadPath.SetVariableByName(Settings,AddressableAssetSettings.kLocalLoadPath);
                Settings.BuildAddressablesWithPlayerBuild=AddressableAssetSettings.PlayerBuildOption.DoNotBuildWithPlayer;
                AddressableAssetSettings.BuildPlayerContent(out var content);
                if(!string.IsNullOrEmpty(content.Error))throw new InvalidOperationException(content.Error);
                var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Boot},locationPathName="Builds/LastLight/M3/LastLight.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                File.WriteAllText("Captures/LastLight/M3/build-result.json",JsonUtility.ToJson(new BuildEvidence{result=result.summary.result.ToString(),errors=(int)result.summary.totalErrors,warnings=(int)result.summary.totalWarnings,size=result.summary.totalSize.ToString(),utc=DateTime.UtcNow.ToString("O")}));
                if(result.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Windows build failed: "+result.summary.result);
                Debug.Log("M2 Windows build succeeded: Builds/LastLight/M3/LastLight.exe");
            }
            finally
            {
                Settings.BuildAddressablesWithPlayerBuild=previousAddressablesMode;
                File.WriteAllBytes(protectedPipeline,protectedPipelineBytes);
                AssetDatabase.ImportAsset(protectedPipeline,ImportAssetOptions.ForceUpdate);
                EditorUtility.SetDirty(Settings);AssetDatabase.SaveAssets();
            }
        }
        [Serializable] private sealed class BuildEvidence {public string result,size,utc;public int errors,warnings;}
    }
}
