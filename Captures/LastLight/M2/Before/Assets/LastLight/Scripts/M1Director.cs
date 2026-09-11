using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight
{
    public sealed class M1Director : MonoBehaviour
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
                if(rootPrefab==null)throw new InvalidOperationException("M1Prototype 缺少 GlobalRoot 引用");
                if(GlobalManager.Instance!=null)throw new InvalidOperationException("请从 M1Prototype 单独启动，已有 LastLight 根节点");
                Global=Instantiate(rootPrefab);await Global.InitializeAsync();
                home=new M1SceneState("Home",this);workshop=new M1SceneState("Workshop",this);
                var flow=Global.Systems.Get<SceneFlowSystem>();flow.Register(home);flow.Register(workshop);
                Global.Systems.Get<InputSystem>().CancelPressed+=Cancel;
                await NewSession();await TravelAsync("Home");
            }
            catch(Exception ex){Report(ex);}
        }
        private async Task NewSession()
        {
            await Global.BeginSessionAsync(s=>{s.Register(new M1EconomySystem());s.Register(new M1ConstructionSystem());s.Register(new M1SurvivalSystem());s.Register(new M1StorySystem());s.Register(new M1ThreatSystem());});
            StartedWallTime=Time.realtimeSinceStartupAsDouble;
        }
        public async Task TravelAsync(string destination)
        {
            if(Busy)return;Busy=true;
            try
            {
                Current?.CloseWindow();
                await Global.UI.OpenAsync("M1Loading",new M1PanelModel{Title="留灯地球",Body=()=>"沿着暖灯前行……"});
                var target=destination=="Home"?home:workshop;
                var result=await Global.Systems.Get<SceneFlowSystem>().GoAsync(target.Id,lifetime.Token);
                if(result==TransitionResult.Busy)return;
                Current=target.Context;
                await Current.ActivateAsync();
                if(destination=="Workshop")Narrative.WorkshopVisited=true;
                Global.Systems.Get<InputSystem>().GameplayEnabled=true;
            }
            catch(Exception ex){Report(ex);throw;}
            finally{Global.UI.Close("M1Loading");Busy=false;}
        }
        public async Task RestartAsync()
        {
            if(Busy)return;
            Busy=true;
            try{Current?.Deactivate();Global.UI.ClearScope(SystemLifetime.Session);await Global.EndSessionAsync();await NewSession();}
            finally{Busy=false;}
            await TravelAsync("Home");
        }
        private void Update()
        {
            if(Busy||Current==null||Global==null||!Global.Ready)return;
            var key=UnityEngine.InputSystem.Keyboard.current;if(key==null)return;
            if(key.bKey.wasPressedThisFrame){Current.ToggleBuild();return;}
            if(key.tabKey.wasPressedThisFrame){Current.OpenInventory();return;}
            if(key.jKey.wasPressedThisFrame)Current.OpenJournal();
        }
        private void Cancel(){if(!Busy&&Current!=null){if(Current.WindowOpen)Current.CloseWindow();else Current.OpenPause();}}
        public async void Run(Task task){try{await task;}catch(OperationCanceledException){/* Closing a loading panel or exiting the session cancels its display intent. */}catch(Exception ex){Report(ex);}}
        private void Report(Exception ex){Failure=ex.Message;Debug.LogException(ex);if(Current!=null&&Current.Ready&&Global!=null&&Global.Ready&&Global.Session!=null)Current.Notify("操作失败："+ex.Message);}
        private void OnGUI(){if(string.IsNullOrEmpty(Failure)||Current!=null)return;GUI.Box(new Rect(30,30,900,100),"M1 启动失败："+Failure+"\n请检查 Console，修复后重新进入 Play Mode。");}
        private void OnDisable(){Current?.Deactivate();}
        private void OnDestroy(){lifetime.Cancel();lifetime.Dispose();Application.runInBackground=priorBackground;if(Global!=null&&Global.Ready)Global.Systems.Get<InputSystem>().CancelPressed-=Cancel;}
    }
    internal sealed class M1SceneState : ISceneState
    {
        private readonly M1Director director;
        public string Id { get; }
        public string Address=>"LastLight/M1/Scenes/"+Id;
        public M1GameSession Context { get; private set; }
        public M1SceneState(string id,M1Director owner){Id=id;director=owner;}
        public Task EnterAsync(Scene scene,CancellationToken cancellation)
        {
            foreach(var root in scene.GetRootGameObjects())foreach(var context in root.GetComponentsInChildren<M1GameSession>(true))
            {if(Context!=null)throw new InvalidOperationException(scene.path+": 会话接线对象重复");Context=context;}
            if(Context==null)throw new InvalidOperationException(scene.path+": 缺少 M1GameSession");
            Context.Bind(director);Context.ValidateConfiguration();return Task.CompletedTask;
        }
        public Task ExitAsync(){if(Context!=null)Context.Deactivate();Context=null;return Task.CompletedTask;}
    }
}
