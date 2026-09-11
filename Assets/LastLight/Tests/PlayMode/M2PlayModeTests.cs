using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace LastLight.Tests
{
    public sealed class M2PlayModeTests
    {
        private M1Director director;
        private string saveDirectory;private bool priorEnglish;
        private Keyboard keyboardDevice;
        private Mouse mouseDevice;
        private InputSettings originalSettings,isolatedSettings;
        private HideFlags originalSettingsFlags;
        private string originalSettingsPath,originalSettingsJson;
        private static IEnumerator Await(Task task){while(!task.IsCompleted)yield return null;if(task.IsFaulted)throw task.Exception;}
        private static IEnumerator Until(Func<bool> predicate,float timeout=20,[System.Runtime.CompilerServices.CallerLineNumber]int line=0){float deadline=Time.realtimeSinceStartup+timeout;while(!predicate()){if(Time.realtimeSinceStartup>deadline)Assert.Fail("等待运行状态超时，调用行 "+line);yield return null;}}
        [UnitySetUp] public IEnumerator Start()
        {
            saveDirectory=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"M2PlayTests-"+Guid.NewGuid());M1Director.SaveDirectoryOverride=saveDirectory;priorEnglish=L.English;L.English=false;
            originalSettings=UnityEngine.InputSystem.InputSystem.settings;originalSettingsJson=JsonUtility.ToJson(originalSettings);
#if UNITY_EDITOR
            originalSettingsPath=UnityEditor.AssetDatabase.GetAssetPath(originalSettings);
#endif
            originalSettingsFlags=originalSettings.hideFlags;originalSettings.hideFlags|=HideFlags.DontUnloadUnusedAsset;isolatedSettings=Object.Instantiate(originalSettings);isolatedSettings.hideFlags=HideFlags.HideAndDontSave;isolatedSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;UnityEngine.InputSystem.InputSystem.settings=isolatedSettings;
#if UNITY_EDITOR
            isolatedSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var loading=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/LastLight/Scenes/M2Chapter.unity",new LoadSceneParameters(LoadSceneMode.Single));
            while(!loading.isDone)yield return null;
#endif
            yield return Until(()=>{director=Object.FindFirstObjectByType<M1Director>();return director!=null&&director.Current!=null&&director.Current.Ready&&!director.Busy;});
            yield return Await(director.RestartAsync());yield return Until(()=>director.Current.CanSnapshot);
        }
        [UnityTearDown] public IEnumerator Stop()
        {
            if(director!=null&&director.Global!=null){yield return Await(director.Global.ShutdownAsync());Object.Destroy(director.Global.gameObject);}
            if(director!=null)Object.Destroy(director.gameObject);yield return null;yield return null;
            if(keyboardDevice!=null)UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboardDevice);keyboardDevice=null;
            if(mouseDevice!=null)UnityEngine.InputSystem.InputSystem.RemoveDevice(mouseDevice);mouseDevice=null;
            // Single-scene reloads can unload the prior InputSettings object: reload its asset, preserving user values.
#if UNITY_EDITOR
            if(originalSettings==null&&!string.IsNullOrEmpty(originalSettingsPath))originalSettings=UnityEditor.AssetDatabase.LoadAssetAtPath<InputSettings>(originalSettingsPath);
#endif
            if(originalSettings==null){originalSettings=ScriptableObject.CreateInstance<InputSettings>();JsonUtility.FromJsonOverwrite(originalSettingsJson,originalSettings);}
            UnityEngine.InputSystem.InputSystem.settings=originalSettings;originalSettings.hideFlags=originalSettingsFlags;if(isolatedSettings!=null)Object.Destroy(isolatedSettings);
            Time.timeScale=1;M1Director.SaveDirectoryOverride=null;L.English=priorEnglish;if(System.IO.Directory.Exists(saveDirectory))System.IO.Directory.Delete(saveDirectory,true);
        }
        private static Button Button(string text)=>Object.FindObjectsByType<M1Panel>(FindObjectsSortMode.None).SelectMany(p=>p.GetComponentsInChildren<Button>()).First(b=>b.GetComponentInChildren<Text>().text==text);
        private static void Fill(Inventory stock){foreach(Resource r in Enum.GetValues(typeof(Resource)))Assert.True(stock.Add(r,100));}
        private void House()
        {
            var c=director.Construction;var s=director.Economy.Storage;
            for(int x=2;x<=4;x++)for(int z=0;z<=2;z++)Assert.NotNull(c.Grid.Place(Structure.Floor,x,z,0,s,out _));
            for(int x=2;x<=4;x++){Assert.NotNull(c.Grid.Place(Structure.Wall,x,2,0,s,out _));Assert.NotNull(c.Grid.Place(x==3?Structure.Door:Structure.Wall,x,0,2,s,out _));}
            for(int z=0;z<=2;z++){Assert.NotNull(c.Grid.Place(Structure.Wall,2,z,3,s,out _));Assert.NotNull(c.Grid.Place(Structure.Wall,4,z,1,s,out _));}
            Assert.NotNull(c.Grid.Place(Structure.Bed,2,1,0,s,out _));Assert.NotNull(c.Grid.Place(Structure.Storage,4,1,0,s,out _));c.Changed();Assert.True(c.HasHouse);
        }
        [UnityTest] public IEnumerator SaveLoadThreeScenesAndPause()
        {
            director.Economy.Backpack.Add(Resource.Wood,7);director.World.Data.bridge=true;director.World.Data.spring=true;
            yield return Await(director.TravelAsync("Workshop"));yield return Await(director.TravelAsync("Greenhouse"));
            director.Current.OpenJournal();yield return null;float t=director.World.Data.springTime;yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual(t,director.World.Data.springTime);
            Assert.True(director.SaveSlot(1));director.Current.CloseWindow();director.Economy.Backpack.Clear();yield return Await(director.TravelAsync("Home"));yield return Await(director.LoadSlotAsync(1));
            Assert.AreEqual("Greenhouse",director.Current.gameObject.scene.name);Assert.AreEqual(7,director.Economy.Backpack[Resource.Wood]);
            yield return Await(director.TravelAsync("Home"));Assert.True(director.Current.IsHome);
            director.OpenMainMenu();var before=System.IO.File.ReadAllText(director.Saves.PathFor(0));yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual(before,System.IO.File.ReadAllText(director.Saves.PathFor(0)));
        }
        [UnityTest] public IEnumerator PlantsBuildingsSleepAndRestore()
        {
            Fill(director.Economy.Storage);House();var c=director.Construction;var stock=director.Economy.Storage;
            var b=c.Grid.Place(Structure.Planter,-3,2,0,stock,out _);Assert.NotNull(b);c.Changed();director.World.Data.spring=true;
            Assert.True(director.World.Plant(b,0,stock));yield return Until(()=>director.Current.CanSnapshot&&Object.FindObjectsByType<M1Built>(FindObjectsSortMode.None).Any(v=>v.Data?.id==b.id));
            Assert.True(director.Current.SleepUntilMorning());Assert.AreEqual(150,b.growth);Assert.True(director.World.Harvest(b,stock));Assert.False(director.World.Harvest(b,stock));
            director.World.Data.samples[1]=true;Assert.True(director.World.Plant(b,1,stock));Assert.True(director.World.Protect(b,stock));b.health=45;c.Changed();
            yield return Until(()=>director.Current.CanSnapshot);director.Current.OpenJournal();yield return null;Assert.True(director.SaveSlot(2));int id=b.id;
            yield return Await(director.LoadSlotAsync(2));Assert.AreEqual(45,c.Find(id).health);Assert.True(c.Find(id).protectedCrop);Assert.AreEqual(1,c.Find(id).crop);
            director.World.Data.warning=true;Assert.False(director.Current.SleepUntilMorning());
        }
        [UnityTest] public IEnumerator RaidSaveRestoresEnemyAndDamageBudget()
        {
            Fill(director.Economy.Storage);House();var s=director.Narrative.State;s.awake=s.stove=s.core=s.liang=s.reward=s.defenseRead=s.reinforced=true;s.choice=3;
            yield return Until(()=>director.Current.CanSnapshot);yield return Await(director.Current.StartRaidAsync());Assert.True(director.Threat.Active);
            director.Current.OpenJournal();yield return null;director.Threat.Strike();yield return Until(()=>director.Current.CanSnapshot);Assert.True(director.SaveSlot(3),L.Resolve(director.SaveMessage));
            yield return Await(director.LoadSlotAsync(3));Assert.True(director.Threat.Active);Assert.AreEqual(1,director.Threat.Strikes);
            Assert.AreEqual(1,Object.FindObjectsByType<M1Beast>(FindObjectsSortMode.None).Count(b=>b.Raid));
            director.Current.OpenJournal();yield return null;float now=director.Survival.ActiveTime;yield return new WaitForSecondsRealtime(.2f);Assert.AreEqual(now,director.Survival.ActiveTime);
        }
        [UnityTest] public IEnumerator LiangBridgeAndSamples()
        {
            Fill(director.Economy.Storage);House();var c=director.Construction;var s=director.Narrative.State;s.awake=s.stove=s.liang=s.completed=s.raidFinished=true;c.LiangWorking=true;
            Assert.NotNull(c.Grid.Place(Structure.Floor,-3,3,0,director.Economy.Storage,out _));Assert.NotNull(c.Grid.Place(Structure.Workbench,-3,3,0,director.Economy.Storage,out _));c.Changed();
            Assert.True(director.World.OrderBridge(c,director.Narrative,director.Economy.Storage));
            yield return Until(()=>director.World.Data.bridge,40);Assert.True(director.World.Data.spring);
            yield return Await(director.TravelAsync("Greenhouse"));
            foreach(var node in Object.FindObjectsByType<M1Node>(FindObjectsSortMode.None).Where(n=>n.Kind==NodeKind.Sample))
            {
                director.Current.CloseWindow();director.Current.Actor.Teleport(node.transform.position+Vector3.back);yield return null;yield return null;director.Current.UseNode(node);yield return null;
            }
            Assert.True(director.World.Data.samples.All(v=>v));
        }
    }
}
