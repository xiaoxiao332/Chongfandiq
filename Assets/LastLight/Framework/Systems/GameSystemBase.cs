using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LastLight
{
    public enum SystemLifetime { Application, Session, Scene }

    public abstract class GameSystemBase
    {
        public virtual IReadOnlyList<Type> Dependencies => Array.Empty<Type>();
        public virtual Task InitializeAsync(SystemRegistry systems, CancellationToken cancellation) => Task.CompletedTask;
        public virtual Task ShutdownAsync() => Task.CompletedTask;
    }

    public interface IGameTick { void Tick(float deltaTime, float unscaledDeltaTime); }

    /// <summary>一个生命周期一个注册表。依赖只能指向本层或祖先，注册完成后才启动。</summary>
    public sealed class SystemRegistry
    {
        private readonly Dictionary<Type, GameSystemBase> systems = new Dictionary<Type, GameSystemBase>();
        private readonly List<Type> registration = new List<Type>();
        private readonly List<GameSystemBase> started = new List<GameSystemBase>();
        private readonly SystemRegistry parent;
        private bool sealedRegistry;
        public SystemLifetime Lifetime { get; }
        public bool Ready { get; private set; }

        public SystemRegistry(SystemLifetime lifetime, SystemRegistry parent = null)
        {
            if (parent != null && lifetime <= parent.Lifetime)
                throw new ArgumentException("子作用域必须短于父作用域。");
            Lifetime = lifetime;
            this.parent = parent;
        }

        public void Register<T>(T system) where T : GameSystemBase
        {
            if (sealedRegistry) throw new InvalidOperationException("系统注册已结束。");
            if (system == null || system.GetType() != typeof(T)) throw new ArgumentException("按系统具体类型注册。");
            if (Contains(typeof(T))) throw new InvalidOperationException("重复注册系统: " + typeof(T).FullName);
            systems.Add(typeof(T), system);
            registration.Add(typeof(T));
        }

        private bool Contains(Type type) => systems.ContainsKey(type) || (parent != null && parent.Contains(type));
        public T Get<T>() where T : GameSystemBase => (T)Resolve(typeof(T));
        private GameSystemBase Resolve(Type type)
        {
            if (systems.TryGetValue(type, out var system)) return system;
            if (parent != null) return parent.Resolve(type);
            throw new InvalidOperationException("缺失依赖（或依赖了更短生命周期）: " + type.FullName);
        }

        public IReadOnlyList<Type> InitializationOrder()
        {
            var marks = new Dictionary<Type, int>();
            var path = new List<Type>();
            var result = new List<Type>();
            foreach (var type in registration) Visit(type, marks, path, result);
            return result;
        }

        private void Visit(Type type, Dictionary<Type, int> marks, List<Type> path, List<Type> result)
        {
            if (marks.TryGetValue(type, out var mark))
            {
                if (mark == 1) throw new InvalidOperationException("循环依赖: " + string.Join(" -> ", path) + " -> " + type);
                return;
            }
            marks[type] = 1;
            path.Add(type);
            foreach (var dependency in systems[type].Dependencies)
            {
                if (systems.ContainsKey(dependency)) Visit(dependency, marks, path, result);
                else if (parent == null || !parent.Contains(dependency))
                    throw new InvalidOperationException("缺失依赖或生命周期方向错误: " + string.Join(" -> ", path) + " -> " + dependency);
            }
            path.RemoveAt(path.Count - 1);
            marks[type] = 2;
            result.Add(type);
        }

        public async Task InitializeAsync(CancellationToken cancellation)
        {
            if (sealedRegistry) throw new InvalidOperationException("系统注册表不能重复启动。");
            if (parent != null && !parent.Ready) throw new InvalidOperationException("父作用域尚未启动。");
            var order = InitializationOrder();
            sealedRegistry = true;
            try
            {
                foreach (var type in order)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var system = systems[type];
                    started.Add(system); // 失败系统也必须清理部分初始化资源。
                    await system.InitializeAsync(this, cancellation);
                }
                cancellation.ThrowIfCancellationRequested();
                Ready = true;
            }
            catch (Exception failure)
            {
                try { await ShutdownAsync(); }
                catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
                throw;
            }
        }

        public void Tick(float deltaTime, float unscaledDeltaTime)
        {
            if (!Ready) return;
            foreach (var system in started)
                if (system is IGameTick tick) tick.Tick(deltaTime, unscaledDeltaTime);
        }

        public async Task ShutdownAsync()
        {
            Ready = false;
            var errors = new List<Exception>();
            for (int i = started.Count - 1; i >= 0; --i)
                try { await started[i].ShutdownAsync(); } catch (Exception ex) { errors.Add(ex); }
            started.Clear();
            if (errors.Count != 0) throw new AggregateException("系统关闭失败", errors);
        }
    }
}
