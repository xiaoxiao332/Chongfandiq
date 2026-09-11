using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastLight
{
    /// <summary>同一组真实资源验收可由 PlayMode 测试和 Windows 样例命令行运行。</summary>
    public static class ArchitectureSmoke
    {
        private sealed class RejectScene : ISceneState
        {
            public string Id => "Reject";
            public string Address => "LastLight/Scenes/A";
            public Task EnterAsync(Scene scene, CancellationToken cancellation) => throw new InvalidOperationException("expected binding failure");
            public Task ExitAsync() => Task.CompletedTask;
        }
        private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException("Smoke: " + message); }
        private static async Task Settle() { for (int i = 0; i < 4; ++i) await Task.Yield(); }
        public static async Task RunAsync(ArchitectureBootstrap boot)
        {
            float deadline = Time.realtimeSinceStartup + 90;
            while (!boot.Started)
            {
                if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("样例启动超时。");
                await Task.Yield();
            }
            var global = GlobalManager.Instance;
            var ui = global.UI; var resources = global.Systems.Get<ResourceSystem>(); var pause = global.Systems.Get<PauseSystem>();
            var flow = global.Systems.Get<SceneFlowSystem>(); var controller = boot.Controller;
            Require(UnityEngine.Object.FindObjectsByType<GlobalManager>(FindObjectsSortMode.None).Length == 1, "unique manager");
            var scope = new ResourceScope();
            var firstTask = resources.LoadAsync<GameObject>("LastLight/Prefabs/Token", scope);
            var secondTask = resources.LoadAsync<GameObject>("LastLight/Prefabs/Token", scope);
            var first = await firstTask; var second = await secondTask;
            Require(first.Asset == second.Asset, "shared underlying asset");
            first.Dispose(); first.Dispose(); Require(second.Asset != null && scope.Count == 1, "independent idempotent leases");
            scope.Dispose(); Require(scope.Count == 0, "scope releases assets");
            var canceledScope = new ResourceScope();
            var pendingAsset = resources.LoadAsync<GameObject>("LastLight/Prefabs/Token", canceledScope);
            canceledScope.Dispose();
            try { await pendingAsset; } catch (ObjectDisposedException) { }
            Require(canceledScope.Count == 0, "late asset cleans closed scope");

            for (int round = 0; round < 10; ++round)
            {
                await controller.NavigateAsync("A"); Require(controller.Visits == 1, "new session resets visits");
                await controller.RentTokenAsync(); Require(resources.ActiveInstanceCount >= 2, "spawned pool object");
                controller.ReturnTokens();
                var view = new PanelViewModel { Title = "test", Body = "modal lifecycle" };
                var opening = ui.OpenAsync("Pause", view);
                var duplicate = ui.OpenAsync("Pause", view);
                Require(ReferenceEquals(opening, duplicate), "concurrent opens merged");
                var panel = await opening;
                int original = panel.GetInstanceID();
                var rect = (RectTransform)panel.transform;
                Require(rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one && rect.offsetMin == Vector2.zero && rect.offsetMax == Vector2.zero, "full-screen panel");
                Require(pause.Paused && Time.timeScale == 0, "window pauses world");
                var pop = await ui.OpenAsync("Confirm", view);
                Require(!panel.GetComponent<CanvasGroup>().interactable && pop.GetComponent<CanvasGroup>().interactable, "top modal focus");
                Require(ui.CloseTop() && pause.Paused, "closing popup retains pause");
                Require(ui.CloseTop() && !pause.Paused && Time.timeScale == 1, "last pause token restores time");
                var reused = await ui.OpenAsync("Pause", view); Require(reused.GetInstanceID() == original, "panel pool reuses instance");
                ui.Close("Pause");
                var pendingPanel = ui.OpenAsync("Confirm", view); ui.Close("Confirm");
                bool canceled = false; try { await pendingPanel; } catch (OperationCanceledException) { canceled = true; }
                Require(canceled && !pause.Paused, "close in-flight panel suppresses late appearance");
                await controller.NavigateAsync("B"); Require(controller.Visits == 2, "session survives A to B");
                await controller.NavigateAsync("A"); Require(controller.Visits == 3, "session survives return to A");
                await controller.NavigateAsync("Menu"); Require(global.Session == null, "return menu destroys session");
                Require(resources.LoadedSceneCount == 1 && ui.OpenCount == 1, "old scenes and panels released");
            }
            flow.Register(new RejectScene());
            bool rejected = false;
            try { await flow.GoAsync("Reject"); } catch (InvalidOperationException ex) { rejected = ex.Message == "expected binding failure"; }
            Require(rejected && flow.CurrentId == "Menu" && resources.LoadedSceneCount == 1 && !pause.Paused, "binding failure rollback");
            var transition = flow.GoAsync("A");
            Require(await flow.GoAsync("B") == TransitionResult.Busy, "reject overlapping transitions");
            await transition; await controller.NavigateAsync("Menu");
            await controller.NavigateAsync("A"); await controller.NavigateAsync("Menu");
            resources.ClearIdle(); await Settle();
            Require(resources.ActiveInstanceCount == 1 && resources.IdleInstanceCount == 0 && resources.AssetEntryCount == 1, "only menu panel resource remains");
            await boot.StopAsync(); await Settle();
            Require(resources.LoadedSceneCount == 0 && resources.ActiveInstanceCount == 0 && resources.AssetEntryCount == 0, "shutdown releases all resources");
            Require(Time.timeScale == 1, "shutdown restores time");
        }
        public static async Task RunCommandLineAsync(ArchitectureBootstrap boot)
        {
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, "-lastLightSmokeReport");
            if (index < 0 || index + 1 >= args.Length) return;
            string path = args[index + 1];
            try { await RunAsync(boot); File.WriteAllText(path, "PASS\n10 menu/A/B/A/menu rounds; UI, pool, leases, rollback, shutdown validated."); Application.Quit(0); }
            catch (Exception ex) { Debug.LogException(ex); File.WriteAllText(path, "FAIL\n" + ex); Application.Quit(1); }
        }
    }
}
