using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LastLight
{
    [RequireComponent(typeof(UIManager))]
    public sealed class GlobalManager : MonoBehaviour
    {
        public static GlobalManager Instance { get; private set; }
        public SystemRegistry Systems { get; private set; }
        public SystemRegistry Session { get; private set; }
        public UIManager UI { get; private set; }
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private Task startup;
        private Task shutdown;
        private bool stopping;
        public bool Ready => !stopping && Systems != null && Systems.Ready && startup != null && startup.Status == TaskStatus.RanToCompletion;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Instance = null; }
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject); UI = GetComponent<UIManager>();
        }
        public Task InitializeAsync()
        {
            if (Instance != this) throw new InvalidOperationException("重复的 GlobalManager 不可初始化。");
            return startup ??= StartSystems();
        }
        private async Task StartSystems()
        {
            Systems = new SystemRegistry(SystemLifetime.Application);
            Systems.Register(new ResourceSystem()); Systems.Register(new PauseSystem());
            Systems.Register(new InputSystem()); Systems.Register(new SceneFlowSystem());
            await Systems.InitializeAsync(lifetime.Token);
            UI.Initialize(Systems.Get<ResourceSystem>(), Systems.Get<PauseSystem>());
        }
        public async Task BeginSessionAsync(Action<SystemRegistry> register)
        {
            if (Session != null) throw new InvalidOperationException("会话已存在。");
            var candidate = new SystemRegistry(SystemLifetime.Session, Systems);
            register(candidate); await candidate.InitializeAsync(lifetime.Token); Session = candidate;
        }
        public async Task EndSessionAsync()
        {
            if (UI != null) UI.ClearScope(SystemLifetime.Session);
            var previous = Session; Session = null;
            if (previous != null) await previous.ShutdownAsync();
        }
        private void Update()
        {
            if (!Ready) return;
            Systems.Tick(Time.deltaTime, Time.unscaledDeltaTime);
            Session?.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }
        public Task ShutdownAsync() => shutdown ??= StopSystems();
        private async Task StopSystems()
        {
            stopping = true;
            lifetime.Cancel();
            if (startup != null) try { await startup; } catch (Exception ex) { Debug.LogException(ex); }
            try
            {
                try { if (Systems != null && Systems.Ready) await Systems.Get<SceneFlowSystem>().StopContentAsync(); }
                finally { await EndSessionAsync(); }
            }
            finally
            {
                try { if (UI != null) await UI.ShutdownAsync(); }
                finally { if (Systems != null) await Systems.ShutdownAsync(); }
            }
        }
        private async void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            try { await ShutdownAsync(); } catch (Exception ex) { Debug.LogException(ex); }
            finally { lifetime.Dispose(); }
        }
    }
}
