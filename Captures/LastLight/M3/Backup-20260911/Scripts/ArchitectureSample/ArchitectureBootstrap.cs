using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace LastLight
{
    public sealed class ArchitectureBootstrap : MonoBehaviour
    {
        [SerializeField] private GlobalManager rootPrefab;
        [SerializeField] private StartupRecoveryPanel recovery;
        private GlobalManager root;
        private bool starting;
        public SampleController Controller { get; private set; }
        public bool Started { get; private set; }
        public void Configure(GlobalManager prefab, StartupRecoveryPanel recoveryPanel)
        { rootPrefab = prefab; recovery = recoveryPanel; }
        private void Start() { StartRequested(); }
        private async void StartRequested()
        {
            if (starting) return;
            starting = true; recovery.Hide();
            try
            {
#if UNITY_EDITOR
                // Addressables 2.9 在退出播放时通过 delayCall 重置；极速重新进入时必须等它完成。
                var editorReady = new TaskCompletionSource<bool>();
                UnityEditor.EditorApplication.delayCall += () => editorReady.TrySetResult(true);
                await editorReady.Task;
                if (this == null) return;
#endif
                if (GlobalManager.Instance != null && root == null) throw new InvalidOperationException("已有 LastLight 根节点，请从启动场景重新运行。");
                if (root == null)
                {
                    root = Instantiate(rootPrefab); root.name = "LastLight Global";
                    await root.InitializeAsync(); Controller = new SampleController(root);
                }
                await Controller.NavigateAsync("Menu"); Started = true;
                _ = ArchitectureSmoke.RunCommandLineAsync(this);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (root != null && !root.Ready)
                {
                    try { await root.ShutdownAsync(); } catch (Exception cleanup) { Debug.LogException(cleanup); }
                    Destroy(root.gameObject); root = null;
                    await Task.Yield(); await Task.Yield();
                }
                recovery.Present("Startup failed.\n" + ex.Message + "\nFix configuration and retry.", StartRequested);
            }
            finally { starting = false; }
        }
        public async Task StopAsync()
        {
            Controller?.Dispose(); if (root != null) await root.ShutdownAsync(); Started = false;
        }
        private void OnDestroy() { Controller?.Dispose(); }
    }
}
