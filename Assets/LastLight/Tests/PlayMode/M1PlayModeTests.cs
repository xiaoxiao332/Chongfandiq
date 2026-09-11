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
    public sealed class M1PlayModeTests
    {
        private M1Director director;
        private Keyboard keyboardDevice;
        private Mouse mouseDevice;
        private InputSettings originalSettings,isolatedSettings;
        private HideFlags originalSettingsFlags;
        private string originalSettingsPath,originalSettingsJson;
        private static IEnumerator Await(Task task){while(!task.IsCompleted)yield return null;if(task.IsFaulted)throw task.Exception;}
        private static IEnumerator Until(Func<bool> predicate,float timeout=20,[System.Runtime.CompilerServices.CallerLineNumber]int line=0){float deadline=Time.realtimeSinceStartup+timeout;while(!predicate()){if(Time.realtimeSinceStartup>deadline)Assert.Fail("等待运行状态超时，调用行 "+line);yield return null;}}
        [UnitySetUp] public IEnumerator Start()
        {
            originalSettings=UnityEngine.InputSystem.InputSystem.settings;originalSettingsJson=JsonUtility.ToJson(originalSettings);
#if UNITY_EDITOR
            originalSettingsPath=UnityEditor.AssetDatabase.GetAssetPath(originalSettings);
#endif
            originalSettingsFlags=originalSettings.hideFlags;originalSettings.hideFlags|=HideFlags.DontUnloadUnusedAsset;isolatedSettings=Object.Instantiate(originalSettings);isolatedSettings.hideFlags=HideFlags.HideAndDontSave;isolatedSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;UnityEngine.InputSystem.InputSystem.settings=isolatedSettings;
#if UNITY_EDITOR
            isolatedSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var loading=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/LastLight/Scenes/M1Prototype.unity",new LoadSceneParameters(LoadSceneMode.Single));
            while(!loading.isDone)yield return null;
#endif
            yield return Until(()=>{director=Object.FindFirstObjectByType<M1Director>();return director!=null&&director.Current!=null&&director.Current.Ready&&!director.Busy;});
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
            Time.timeScale=1;
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
        [UnityTest] public IEnumerator MovementPauseAndSceneRoundTrip()
        {
            var session=director.Current;var keyboard=InputSystemDevice();var before=session.Actor.transform.position;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSeconds(.4f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
            Assert.Greater(Vector3.Distance(before,session.Actor.transform.position),.2f,"实际输入应移动角色");
            session.OpenJournal();yield return Until(()=>director.Global.UI.HasModal);float clock=session.ActiveTime;var position=session.Actor.transform.position;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.W));yield return new WaitForSecondsRealtime(.3f);Assert.AreEqual(clock,session.ActiveTime,.01f);Assert.Less(Vector3.Distance(position,session.Actor.transform.position),.01f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.1f);UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));yield return new WaitForSecondsRealtime(.15f);UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return Until(()=>!session.WindowOpen);Assert.False(director.Global.Systems.Get<PauseSystem>().Paused);
            director.Economy.Backpack.Add(Resource.Wood,7);director.Narrative.State.core=true;
            yield return Await(director.TravelAsync("Workshop"));Assert.False(director.Current.IsHome);Assert.AreEqual(7,director.Economy.Backpack[Resource.Wood]);Assert.True(director.Narrative.State.core);
            yield return Await(director.TravelAsync("Home"));Assert.True(director.Current.IsHome);Assert.AreEqual(7,director.Economy.Backpack[Resource.Wood]);
            yield return Await(director.RestartAsync());Assert.AreEqual(0,director.Economy.Backpack[Resource.Wood]);Assert.False(director.Narrative.State.core);
        }
        private Keyboard InputSystemDevice(){keyboardDevice=UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>("M1TestKeyboard");keyboardDevice.MakeCurrent();return keyboardDevice;}
        private IEnumerator PointerClick(Vector3 world)
        {
            Vector2 point=Camera.main.WorldToScreenPoint(world);
            Assert.That(point.x,Is.InRange(Screen.width*.27f,Screen.width*.73f),"测试光标不能落在两侧 UI 上");Assert.That(point.y,Is.InRange(10f,Screen.height-10f));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouseDevice,new MouseState{position=point});yield return new WaitForSecondsRealtime(.1f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouseDevice,new MouseState{position=point,buttons=1});yield return new WaitForSecondsRealtime(.15f);
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(mouseDevice,new MouseState{position=point});yield return new WaitForSecondsRealtime(.1f);
        }
        [UnityTest] public IEnumerator PointerBuildRotateAndRemove()
        {
            Fill(director.Economy.Storage);var session=director.Current;var keyboard=InputSystemDevice();mouseDevice=UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>("M1TestMouse");mouseDevice.MakeCurrent();
            session.ToggleBuild();yield return Until(()=>director.Global.Systems.Get<PauseSystem>().Paused);yield return new WaitForSecondsRealtime(.15f);
            yield return PointerClick(new Vector3(-6,0,-6));Assert.True(director.Construction.Grid.IsFloor(-3,-3));Assert.AreEqual(1,director.Construction.Grid.Items.Count,"单次鼠标按下只放一个地板");
            Button("墙").onClick.Invoke();UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.R));yield return new WaitForSecondsRealtime(.15f);UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return PointerClick(new Vector3(-6,0,-6));var wall=director.Construction.Grid.Items.Single(b=>b.type==Structure.Wall);Assert.AreEqual(1,wall.rotation,"一次 R 按下只旋转90度");
            Button("拆除模式").onClick.Invoke();yield return new WaitForSecondsRealtime(.1f);yield return PointerClick(new Vector3(-5,1.2f,-6));Assert.False(director.Construction.Grid.Items.Contains(wall));Assert.AreEqual(98,director.Economy.Storage[Resource.Wood]);
            session.CloseWindow();yield return null;
        }
        [UnityTest] public IEnumerator BuildingDamageRepairAndLiangOperateInScene()
        {
            Fill(director.Economy.Storage);House();var session=director.Current;
            yield return Until(()=>Object.FindObjectsByType<M1Built>(FindObjectsSortMode.None).Count(b=>b.Data!=null)==director.Construction.Grid.Items.Count);
            var door=director.Construction.Grid.Items.First(b=>b.type==Structure.Door);session.DamageBuilding(door.id,100);
            var view=Object.FindObjectsByType<M1Built>(FindObjectsSortMode.None).First(b=>b.Data?.id==door.id);Assert.True(view.GetComponentsInChildren<Collider>().All(c=>!c.enabled));
            director.Narrative.State.liang=true;director.Construction.LiangWorking=true;
            yield return Until(()=>door.health==100,35);Assert.True(view.GetComponentsInChildren<Collider>().All(c=>c.enabled));Assert.AreEqual(1,director.Construction.LiangRepairs);
            session.ToggleBuild();yield return Until(()=>session.BuildingMode&&director.Global.Systems.Get<PauseSystem>().Paused);session.CloseWindow();yield return null;Assert.False(director.Global.Systems.Get<PauseSystem>().Paused);
        }
        [UnityTest] public IEnumerator NavigationTracksDamageRepairAndPause()
        {
            Fill(director.Economy.Storage);House();var session=director.Current;
            yield return Until(()=>Object.FindObjectsByType<M1Built>(FindObjectsSortMode.None).Count(b=>b.Data!=null)==director.Construction.Grid.Items.Count);
            yield return new WaitForSeconds(.3f);
            Assert.True(UnityEngine.AI.NavMesh.SamplePosition(new Vector3(6,0,3.5f),out var inside,.5f,UnityEngine.AI.NavMesh.AllAreas));
            Assert.True(UnityEngine.AI.NavMesh.SamplePosition(new Vector3(6,0,6.5f),out var outside,.5f,UnityEngine.AI.NavMesh.AllAreas));
            Assert.True(UnityEngine.AI.NavMesh.Raycast(inside.position,outside.position,out _,UnityEngine.AI.NavMesh.AllAreas),"Intact wall must block navigation");
            var wall=director.Construction.Grid.Items.First(b=>b.type==Structure.Wall&&b.x==3&&b.z==2&&b.rotation==0);
            session.DamageBuilding(wall.id,100);yield return new WaitForSeconds(.3f);
            Assert.False(UnityEngine.AI.NavMesh.Raycast(inside.position,outside.position,out _,UnityEngine.AI.NavMesh.AllAreas),"Destroyed wall must open navigation");
            Assert.True(director.Construction.BeginRepair(wall,director.Economy.Storage));
            Assert.True(director.Construction.TickRepair(8,director.Economy.Storage,true,false));yield return new WaitForSeconds(.5f);
            Assert.True(UnityEngine.AI.NavMesh.Raycast(inside.position,outside.position,out _,UnityEngine.AI.NavMesh.AllAreas),"Repair must restore obstacle");
            var nav=Object.FindObjectsByType<M1Navigator>(FindObjectsSortMode.None).Single();Assert.True(nav.Available);
            Assert.True(nav.Go(new Vector3(-10,0,-10)));yield return new WaitForSeconds(.2f);
            session.OpenJournal();yield return Until(()=>director.Global.UI.HasModal);yield return null;var position=nav.transform.position;
            yield return new WaitForSecondsRealtime(.25f);Assert.Less(Vector3.Distance(position,nav.transform.position),.01f,"Paused agent must remain still");session.CloseWindow();
        }
        [UnityTest] public IEnumerator ThreeBranchesAndRaidSettlementReachConclusion()
        {
            for(int choice=1;choice<=3;choice++)
            {
                if(choice>1)yield return Await(director.RestartAsync());
                Fill(director.Economy.Storage);House();var state=director.Narrative.State;state.awake=true;state.stove=true;state.core=true;state.shortcut=true;state.liang=true;Assert.True(state.ClaimReward(director.Economy.Storage));
                var session=director.Current;session.Actor.Teleport(new Vector3(0,0,-11));session.InteractNearest();yield return Until(()=>director.Global.UI.HasModal);
                Button(choice==1?"援助：给两份零件":choice==2?"交换：一份并承诺床位":"拒绝：留下守住这里").onClick.Invoke();yield return null;Assert.AreEqual(choice,state.choice);Assert.True(state.shortcut);
                if(choice==2){yield return Until(()=>!session.Paused);session.InteractNearest();yield return Until(()=>director.Global.UI.HasModal);Button("履行床位承诺").onClick.Invoke();Assert.AreEqual(3,state.promise);session.CloseWindow();}
                state.defenseRead=true;var barricade=director.Construction.Grid.Place(Structure.Barricade,2,-5,0,director.Economy.Storage,out _);Assert.NotNull(barricade);director.Construction.Changed();
                yield return Await(session.StartRaidAsync());Assert.True(session.RaidActive);session.ToggleBuild();Assert.False(session.BuildingMode);
                if(choice==2){session.RecordRaidStrike();session.RecordRaidStrike();session.RecordRaidStrike();}
                else {var beast=Object.FindObjectsByType<M1Beast>(FindObjectsSortMode.None).First(b=>b.Raid);session.Actor.Teleport(beast.transform.position+Vector3.back);for(int hit=0;hit<3;hit++)session.Melee(session.Actor.transform.position,Vector3.forward);}
                yield return Until(()=>state.raidFinished);Assert.AreEqual(choice==2,state.raidFailed);
                if(choice==2){director.Construction.LiangWorking=true;yield return Until(()=>director.Construction.Grid.Items.All(b=>!b.Damaged),50);}
                session.Actor.Teleport(new Vector3(0,0,-11));session.InteractNearest();yield return Until(()=>director.Global.UI.HasModal);Button("阅读后果 / 收束").onClick.Invoke();yield return Until(()=>state.completed);Assert.True(state.consequence);session.CloseWindow();
            }
        }
    }
}
