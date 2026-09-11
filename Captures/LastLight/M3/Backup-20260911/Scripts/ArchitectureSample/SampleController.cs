using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight
{
    public sealed class SampleSessionSystem : GameSystemBase
    {
        public int Visits { get; private set; }
        public void RecordVisit() => Visits++;
    }

    public sealed class SampleSceneSystem : GameSystemBase
    {
        public ResourceScope Resources { get; } = new ResourceScope();
        public override Task ShutdownAsync() { Resources.Dispose(); return Task.CompletedTask; }
    }

    public sealed class SampleSceneState : ISceneState
    {
        private readonly GlobalManager global;
        private SystemRegistry registry;
        public string Id { get; }
        public string Address => "LastLight/Scenes/" + Id;
        public SampleSceneContext Context { get; private set; }
        public ResourceScope Scope => registry.Get<SampleSceneSystem>().Resources;
        public SampleSceneState(string id, GlobalManager global) { Id = id; this.global = global; }
        public async Task EnterAsync(Scene scene, CancellationToken cancellation)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var context = root.GetComponentInChildren<SampleSceneContext>(true);
                if (context == null) continue;
                if (Context != null) throw new InvalidOperationException("场景存在重复 SampleSceneContext: " + scene.path);
                Context = context;
            }
            if (Context == null || Context.StateId != Id || Context.SpawnRoot == null || Context.SceneCamera == null)
                throw new InvalidOperationException("样例场景缺少必要引用: " + scene.path);
            registry = new SystemRegistry(SystemLifetime.Scene, global.Session ?? global.Systems);
            registry.Register(new SampleSceneSystem()); await registry.InitializeAsync(cancellation);
        }
        public async Task ExitAsync()
        {
            try { if (registry != null) await registry.ShutdownAsync(); }
            finally { registry = null; Context = null; }
        }
    }

    /// <summary>样例用例协调；UI 只收到展示模型及命令，不获取全局系统。</summary>
    public sealed class SampleController : IDisposable
    {
        private readonly GlobalManager global;
        private readonly SceneFlowSystem flow;
        private readonly ResourceSystem resources;
        private readonly InputSystem input;
        private readonly Dictionary<string, SampleSceneState> states = new Dictionary<string, SampleSceneState>();
        private readonly List<InstanceLease> tokens = new List<InstanceLease>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private PanelViewModel hud;
        private bool busy;
        public bool Busy => busy;
        public string Current => flow.CurrentId;
        public int Visits => global.Session?.Get<SampleSessionSystem>().Visits ?? 0;
        public SampleController(GlobalManager global)
        {
            this.global = global; flow = global.Systems.Get<SceneFlowSystem>();
            resources = global.Systems.Get<ResourceSystem>(); input = global.Systems.Get<InputSystem>();
            foreach (var id in new[] { "Menu", "A", "B" })
            {
                var state = new SampleSceneState(id, global); states.Add(id, state); flow.Register(state);
            }
            input.CancelPressed += Cancel;
        }
        public async Task NavigateAsync(string id)
        {
            if (busy) return;
            busy = true; bool createdSession = false;
            try
            {
                await global.UI.OpenAsync("Loading", new PanelViewModel { Title = "LAST LIGHT", Body = "Loading " + id + "..." });
                if (id != "Menu" && global.Session == null)
                {
                    await global.BeginSessionAsync(s => s.Register(new SampleSessionSystem())); createdSession = true;
                }
                var result = await flow.GoAsync(id, lifetime.Token);
                if (result == TransitionResult.Busy) return;
                if (result == TransitionResult.Completed)
                {
                    tokens.Clear(); // 旧场景作用域已经归还实例。
                    global.UI.ClearScope(SystemLifetime.Scene);
                    if (id == "Menu") await global.EndSessionAsync();
                    else global.Session.Get<SampleSessionSystem>().RecordVisit();
                }
                input.GameplayEnabled = id != "Menu";
                await ShowCurrent();
            }
            catch
            {
                if (createdSession && flow.CurrentId == "Menu") await global.EndSessionAsync();
                throw;
            }
            finally { global.UI.Close("Loading"); busy = false; }
        }
        private async Task ShowCurrent()
        {
            if (Current == "Menu")
            {
                await global.UI.OpenAsync("Menu", new PanelViewModel
                {
                    Title = "LAST LIGHT / ARCHITECTURE",
                    Body = "Persistent systems. Scene-owned resources.\nPrefab-authored, full-screen panels.\nStart a session to explore A and B.",
                    Labels = new[] { "Start session", "Exit sample" },
                    Command = i => i == 0 ? NavigateAsync("A") : ExitAsync()
                });
            }
            else
            {
                hud = new PanelViewModel
                {
                    Title = "LAST LIGHT / " + Current,
                    Labels = new[] { "Travel to " + (Current == "A" ? "B" : "A"), "Pause window", "Rent object", "Return objects", "Back to menu" },
                    Command = HudCommand
                };
                RefreshHud(); await global.UI.OpenAsync("HUD", hud);
            }
        }
        private Task HudCommand(int index)
        {
            switch (index)
            {
                case 0: return NavigateAsync(Current == "A" ? "B" : "A");
                case 1: return ShowPause();
                case 2: return RentTokenAsync();
                case 3: ReturnTokens(); return Task.CompletedTask;
                case 4: return NavigateAsync("Menu");
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
        public async Task RentTokenAsync()
        {
            if (busy || Current == null || Current == "Menu") return;
            var state = states[Current];
            var lease = await resources.RentAsync("LastLight/Prefabs/Token", state.Context.SpawnRoot, state.Scope, 8, lifetime.Token);
            lease.Instance.transform.localPosition = new Vector3((tokens.Count % 5 - 2) * 1.5f, 1, tokens.Count / 5 * 1.5f);
            lease.Instance.SetActive(true); tokens.Add(lease); RefreshHud();
        }
        public void ReturnTokens()
        {
            foreach (var token in tokens) token.Dispose(); tokens.Clear(); RefreshHud();
        }
        private void RefreshHud()
        {
            if (hud == null) return;
            hud.Body = "Session visits: " + Visits + "\nObjects here: " + tokens.Count + "\nEsc closes the top panel.\nObjects stop rotating while paused.";
            hud.Refresh();
        }
        public Task<PanelBase> ShowPause() => global.UI.OpenAsync("Pause", new PanelViewModel
        {
            Title = "PAUSED / WINDOW", Body = "World paused. UI remains responsive.",
            Labels = new[] { "Resume", "Open confirmation" },
            Command = i =>
            {
                if (i == 0) { global.UI.Close("Pause"); return Task.CompletedTask; }
                return global.UI.OpenAsync("Confirm", new PanelViewModel
                {
                    Title = "CONFIRM / POP", Body = "Only this top modal receives input.\nClosing it keeps the underlying window paused.",
                    Labels = new[] { "Close popup" }, Command = _ => { global.UI.Close("Confirm"); return Task.CompletedTask; }
                });
            }
        });
        private async void Cancel()
        {
            try
            {
                if (busy) return;
                if (!global.UI.CloseTop() && Current != null && Current != "Menu") await ShowPause();
            }
            catch (Exception ex) { Debug.LogException(ex); }
        }
        private async Task ExitAsync()
        {
            Dispose(); await global.ShutdownAsync();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
        public void Dispose() { input.CancelPressed -= Cancel; lifetime.Cancel(); }
    }
}
