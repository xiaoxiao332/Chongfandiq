using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.SceneManagement;

namespace LastLight
{
    public interface ISceneState
    {
        string Id { get; }
        string Address { get; }
        Task EnterAsync(Scene scene, CancellationToken cancellation);
        Task ExitAsync();
    }
    public enum TransitionResult { Completed, Busy, Unchanged }
    public sealed class SceneFlowSystem : GameSystemBase
    {
        private readonly Dictionary<string, ISceneState> states = new Dictionary<string, ISceneState>();
        private ResourceSystem resources;
        private InputSystem input;
        private PauseSystem pause;
        private SceneLease currentScene;
        private ISceneState current;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private Task<TransitionResult> transition;
        private Task stopping;
        public string CurrentId => current?.Id;
        public bool Busy => transition != null && !transition.IsCompleted;
        public override IReadOnlyList<Type> Dependencies => new[] { typeof(ResourceSystem), typeof(InputSystem), typeof(PauseSystem) };
        public override Task InitializeAsync(SystemRegistry systems, CancellationToken cancellation)
        {
            resources = systems.Get<ResourceSystem>(); input = systems.Get<InputSystem>(); pause = systems.Get<PauseSystem>();
            return Task.CompletedTask;
        }
        public void Register(ISceneState state)
        {
            if (states.ContainsKey(state.Id)) throw new InvalidOperationException("重复场景状态: " + state.Id);
            states.Add(state.Id, state);
        }
        public Task<TransitionResult> GoAsync(string id, CancellationToken cancellation = default)
        {
            if (stopping != null) throw new ObjectDisposedException(nameof(SceneFlowSystem));
            if (Busy) return Task.FromResult(TransitionResult.Busy);
            if (CurrentId == id) return Task.FromResult(TransitionResult.Unchanged);
            if (!states.TryGetValue(id, out var state)) throw new ArgumentException("未知场景状态: " + id);
            transition = Transition(state, cancellation); return transition;
        }
        private async Task<TransitionResult> Transition(ISceneState target, CancellationToken cancellation)
        {
            await Task.Yield();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellation);
            using var paused = pause.Acquire();
            bool priorInput = input.GameplayEnabled; input.GameplayEnabled = false;
            SceneLease incoming = null; bool entered = false; bool committed = false;
            try
            {
                incoming = await resources.LoadSceneAsync(target.Address, linked.Token);
                entered = true;
                await target.EnterAsync(incoming.Scene, linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                var previous = current; var previousScene = currentScene;
                SceneManager.SetActiveScene(incoming.Scene);
                current = target; currentScene = incoming; committed = true;
                // 提交后清理旧场景；若清理失败，保留新状态并向调用方报告，不能伪造回滚。
                try { if (previous != null) await previous.ExitAsync(); }
                finally { if (previousScene != null) await resources.UnloadSceneAsync(previousScene); }
                return TransitionResult.Completed;
            }
            catch
            {
                if (!committed)
                {
                    try { if (entered) await target.ExitAsync(); }
                    finally { if (incoming != null) await resources.UnloadSceneAsync(incoming); }
                    if (currentScene != null) SceneManager.SetActiveScene(currentScene.Scene);
                }
                throw;
            }
            finally { input.GameplayEnabled = priorInput; }
        }
        public Task StopContentAsync() => stopping ??= StopContent();
        private async Task StopContent()
        {
            lifetime.Cancel();
            var errors = new List<Exception>();
            if (transition != null)
                try { await transition; } catch (OperationCanceledException) { }
                catch (Exception ex) { errors.Add(ex); }
            try { if (current != null) await current.ExitAsync(); } catch (Exception ex) { errors.Add(ex); }
            try { if (currentScene != null) await resources.UnloadSceneAsync(currentScene); } catch (Exception ex) { errors.Add(ex); }
            current = null; currentScene = null; states.Clear(); lifetime.Dispose();
            if (errors.Count != 0) throw new AggregateException("场景流程关闭时发生错误，已继续执行清理。", errors);
        }
        public override Task ShutdownAsync() => StopContentAsync();
    }
}
