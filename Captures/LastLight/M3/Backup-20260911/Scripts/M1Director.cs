using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight
{
    public sealed partial class M1Director : MonoBehaviour
    {
        [SerializeField] private GlobalManager rootPrefab;
        public GlobalManager Global { get; private set; }
        public M1GameSession Current { get; private set; }
        public bool Busy { get; private set; }
        public string Failure { get; private set; }
        private M1SceneState home,workshop;
        private readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        private bool priorBackground;
        public double StartedWallTime { get; private set; }
        public M1EconomySystem Economy=>Global.Session.Get<M1EconomySystem>();
        public M1ConstructionSystem Construction=>Global.Session.Get<M1ConstructionSystem>();
        public M1SurvivalSystem Survival=>Global.Session.Get<M1SurvivalSystem>();
        public M1StorySystem Narrative=>Global.Session.Get<M1StorySystem>();
        public M1ThreatSystem Threat=>Global.Session.Get<M1ThreatSystem>();
        public void Configure(GlobalManager root){rootPrefab=root;}
        private async void Start()
        {
            try
            {
                priorBackground=Application.runInBackground;Application.runInBackground=true;
#if UNITY_EDITOR
                var ready=new TaskCompletionSource<bool>();UnityEditor.EditorApplication.delayCall+=()=>ready.TrySetResult(true);await ready.Task;
                if(this==null)return;
#endif
                if(rootPrefab==null)throw new InvalidOperationException(L.K("t108eb7aa9c"));
                if(GlobalManager.Instance!=null)throw new InvalidOperationException(L.K("t9b4073b64a"));
                Global=Instantiate(rootPrefab);await Global.InitializeAsync();
                home=new M1SceneState("Home",this);workshop=new M1SceneState("Workshop",this);
                var flow=Global.Systems.Get<SceneFlowSystem>();flow.Register(home);flow.Register(workshop);
                Global.Systems.Get<InputSystem>().CancelPressed+=Cancel;
                if(m2)InitializeM2();
                AtMainMenu=m2;await NewSession();await TravelAsync("Home");if(m2)OpenMainMenu();
            }
            catch(Exception ex){Report(ex);}
        }
        private async Task NewSession()
        {
            await Global.BeginSessionAsync(s=>{s.Register(new M1EconomySystem());s.Register(new M1ConstructionSystem());s.Register(new M1SurvivalSystem());s.Register(new M1StorySystem());s.Register(new M1ThreatSystem());if(m2)s.Register(new M2WorldSystem());});
            StartedWallTime=Time.realtimeSinceStartupAsDouble;
        }
        public async Task TravelAsync(string destination)
        {
            if(Busy)return;Busy=true;
            try
            {
                if(m2&&!Restoring&&Current!=null&&Current.Ready)Current.RememberEnemies();
                if(m2&&!Restoring&&destination!="Home"&&World.Data.warning)World.Data.warningAt=-1;
                Current?.CloseWindow();
                await Global.UI.OpenAsync("M1Loading",new M1PanelModel{Title=L.K("td3f9844799"),Body=()=>L.K("t45ea2ef541")});
                var target=destination=="Home"?home:destination=="Workshop"?workshop:destination=="Greenhouse"&&m2?greenhouse:throw new ArgumentException("Unknown destination: "+destination);
                var result=await Global.Systems.Get<SceneFlowSystem>().GoAsync(target.Id,lifetime.Token);
                if(result==TransitionResult.Busy)return;
                Current=target.Context;
                await Current.ActivateAsync();
                if(destination=="Workshop")Narrative.WorkshopVisited=true;
                Global.Systems.Get<InputSystem>().GameplayEnabled=true;
            }
            catch(Exception ex){Report(ex);throw;}
            finally{Global.UI.Close("M1Loading");Busy=false;}
            if(m2&&!Restoring)AutoSave();
        }
        public async Task RestartAsync()
        {
            if(Busy)return;
            AtMainMenu=false;Busy=true;
            try{if(Current!=null)await Current.QuiesceAsync();Global.UI.ClearScope(SystemLifetime.Session);await Global.EndSessionAsync();savedEnemies.Clear();await NewSession();}
            finally{Busy=false;}
            await TravelAsync("Home");
        }
        private void Update()
        {
            PumpAutoSave();
            if(Busy||Restoring||AtMainMenu||Current==null||Global==null||!Global.Ready||Current.Actor.IsRebinding)return;
            if(m2){if(Current.Actor.Pressed("Build"))Current.ToggleBuild();else if(Current.Actor.Pressed("Bag"))Current.OpenInventory();else if(Current.Actor.Pressed("Journal"))Current.OpenJournal();return;}
            var key=UnityEngine.InputSystem.Keyboard.current;if(key==null)return;
            if(key.bKey.wasPressedThisFrame){Current.ToggleBuild();return;}
            if(key.tabKey.wasPressedThisFrame){Current.OpenInventory();return;}
            if(key.jKey.wasPressedThisFrame)Current.OpenJournal();
        }
        private void Cancel(){if(!Busy&&!Restoring&&!AtMainMenu&&Current!=null&&!Current.Actor.IsRebinding){if(Current.WindowOpen)Current.CloseWindow();else Current.OpenPause();}}
        public async void Run(Task task){try{await task;}catch(OperationCanceledException){/* Closing a loading panel or exiting the session cancels its display intent. */}catch(Exception ex){Report(ex);}}
        private void Report(Exception ex){Failure=ex.Message;Debug.LogException(ex);if(Current!=null&&Current.Ready&&Global!=null&&Global.Ready&&Global.Session!=null)Current.Notify(L.K("t648f281592")+ex.Message);}
        private void OnGUI(){if(string.IsNullOrEmpty(Failure)||Current!=null)return;GUI.Box(new Rect(30,30,900,100),L.K("taf4308792e")+Failure+L.K("tba4b6e0001"));}
        private void OnDisable(){Current?.Deactivate();}
        private void OnDestroy(){lifetime.Cancel();lifetime.Dispose();Application.runInBackground=priorBackground;if(Global!=null&&Global.Ready)Global.Systems.Get<InputSystem>().CancelPressed-=Cancel;}
    }
    internal sealed class M1SceneState : ISceneState
    {
        private readonly M1Director director;
        public string Id { get; }
        public string Address=>director.Prefix+"Scenes/"+Id;
        public M1GameSession Context { get; private set; }
        public M1SceneState(string id,M1Director owner){Id=id;director=owner;}
        public Task EnterAsync(Scene scene,CancellationToken cancellation)
        {
            foreach(var root in scene.GetRootGameObjects())foreach(var context in root.GetComponentsInChildren<M1GameSession>(true))
            {if(Context!=null)throw new InvalidOperationException(scene.path+L.K("t535110db4b"));Context=context;}
            if(Context==null)throw new InvalidOperationException(scene.path+L.K("t2f8c9fad99"));
            Context.Bind(director);Context.ValidateConfiguration();return Task.CompletedTask;
        }
        public async Task ExitAsync(){if(Context!=null)await Context.QuiesceAsync();Context=null;}
    }
}
