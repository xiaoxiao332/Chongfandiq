using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace LastLight
{
    public interface IPoolable
    {
        void OnRent();
        void OnReturn();
    }

    public sealed class InstanceLease : IDisposable
    {
        private ResourceSystem owner;
        private readonly ResourceScope scope;
        internal readonly string Key;
        private GameObject instance;
        public GameObject Instance => owner != null ? instance : throw new ObjectDisposedException(nameof(InstanceLease));
        internal InstanceLease(ResourceSystem owner, string key, GameObject instance, ResourceScope scope)
        { this.owner = owner; Key = key; this.instance = instance; this.scope = scope; }
        public void Dispose()
        {
            if (owner == null) return;
            var system = owner; owner = null; scope?.Forget(this);
            system.ReturnInternal(this, instance); instance = null;
        }
        internal bool OwnedBy(ResourceSystem system) => owner == system;
    }

    public sealed class SceneLease
    {
        internal AsyncOperationHandle<SceneInstance> Handle;
        internal ResourceSystem Owner;
        internal Task Unloading;
        public Scene Scene => Owner != null ? Handle.Result.Scene : throw new ObjectDisposedException(nameof(SceneLease));
    }

    /// <summary>Addressables 唯一边界。调用须在 Unity 主线程；异步操作不阻塞主线程。</summary>
    public sealed class ResourceSystem : GameSystemBase
    {
        private sealed class Entry
        {
            public AsyncOperationHandle Handle;
            public int References;
        }
        private sealed class Pool
        {
            public Task<ResourceLease<GameObject>> Prefab;
            public readonly Stack<GameObject> Idle = new Stack<GameObject>();
            public int Active;
            public int Capacity;
        }
        private readonly Dictionary<(string, Type), Entry> assets = new Dictionary<(string, Type), Entry>();
        private readonly Dictionary<string, Pool> pools = new Dictionary<string, Pool>();
        private readonly HashSet<InstanceLease> instances = new HashSet<InstanceLease>();
        private readonly HashSet<IDisposable> assetLeases = new HashSet<IDisposable>();
        private readonly HashSet<SceneLease> scenes = new HashSet<SceneLease>();
        private readonly List<Task> pending = new List<Task>();
        private readonly ResourceScope applicationResources = new ResourceScope();
        private GameObject poolRoot;
        private bool stopping;
        public int AssetEntryCount => assets.Count;
        public int ActiveInstanceCount => instances.Count;
        public int LoadedSceneCount => scenes.Count;
        public int IdleInstanceCount { get { int count = 0; foreach (var p in pools.Values) count += p.Idle.Count; return count; } }

        public override async Task InitializeAsync(SystemRegistry systems, CancellationToken cancellation)
        {
            var initialization = Addressables.InitializeAsync(false);
            try { await initialization.Task; cancellation.ThrowIfCancellationRequested(); }
            finally { if (initialization.IsValid()) Addressables.Release(initialization); }
            poolRoot = new GameObject("LastLight Pool (inactive)");
            poolRoot.SetActive(false);
            Object.DontDestroyOnLoad(poolRoot);
        }

        public Task<ResourceLease<T>> LoadAsync<T>(string key, ResourceScope scope, CancellationToken cancellation = default) where T : Object
        {
            CheckOpen(scope, cancellation);
            var task = LoadCore<T>(key, scope, cancellation);
            Track(task);
            return task;
        }
        private void Track(Task task)
        {
            pending.RemoveAll(t => t.IsCompleted);
            pending.Add(task);
        }
        private void CheckOpen(ResourceScope scope, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (stopping || (scope != null && scope.IsClosed)) throw new ObjectDisposedException(nameof(ResourceSystem));
        }
        private async Task<ResourceLease<T>> LoadCore<T>(string key, ResourceScope scope, CancellationToken cancellation) where T : Object
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("资源键不能为空。", nameof(key));
            var id = (key, typeof(T));
            if (!assets.TryGetValue(id, out var entry))
            {
                entry = new Entry { Handle = Addressables.LoadAssetAsync<T>(key) };
                assets.Add(id, entry);
            }
            entry.References++;
            bool transferred = false;
            try
            {
                await entry.Handle.Task;
                CheckOpen(scope, cancellation);
                if (entry.Handle.Status != AsyncOperationStatus.Succeeded)
                    throw new InvalidOperationException("资源加载失败: " + key, entry.Handle.OperationException);
                ResourceLease<T> lease = null;
                lease = new ResourceLease<T>((T)entry.Handle.Result, scope ?? applicationResources, () => { assetLeases.Remove(lease); Release(id, entry); });
                assetLeases.Add(lease);
                transferred = true;
                (scope ?? applicationResources).Track(lease);
                return lease;
            }
            finally { if (!transferred) Release(id, entry); }
        }
        private void Release((string, Type) id, Entry entry)
        {
            if (--entry.References != 0) return;
            assets.Remove(id);
            if (entry.Handle.IsValid()) Addressables.Release(entry.Handle);
        }

        public Task<InstanceLease> RentAsync(string key, Transform parent, ResourceScope scope, int idleCapacity = 4, CancellationToken cancellation = default)
        {
            CheckOpen(scope, cancellation);
            if (idleCapacity < 0) throw new ArgumentOutOfRangeException(nameof(idleCapacity));
            var task = RentCore(key, parent, scope, idleCapacity, cancellation);
            Track(task);
            return task;
        }
        private async Task<InstanceLease> RentCore(string key, Transform parent, ResourceScope scope, int capacity, CancellationToken cancellation)
        {
            if (!pools.TryGetValue(key, out var pool))
            {
                pool = new Pool { Capacity = capacity, Prefab = LoadAsync<GameObject>(key, null) };
                pools.Add(key, pool);
            }
            else if (pool.Capacity != capacity) throw new InvalidOperationException("同一资源池容量必须一致: " + key);
            pool.Active++;
            GameObject instance = null;
            bool transferred = false;
            try
            {
                var prefab = await pool.Prefab;
                CheckOpen(scope, cancellation);
                while (pool.Idle.Count > 0 && instance == null) instance = pool.Idle.Pop();
                if (instance == null) instance = Object.Instantiate(prefab.Asset, poolRoot.transform, false);
                instance.SetActive(false);
                instance.transform.SetParent(parent, false);
                foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    if (component is IPoolable reusable) reusable.OnRent();
                var lease = new InstanceLease(this, key, instance, scope ?? applicationResources);
                instances.Add(lease);
                transferred = true;
                (scope ?? applicationResources).Track(lease);
                return lease; // 消费者初始化后显式激活，避免 Awake/OnEnable 先使用未绑定依赖。
            }
            finally
            {
                if (!transferred)
                {
                    if (instance != null) Object.Destroy(instance);
                    pool.Active--;
                    Trim(key, pool);
                }
            }
        }
        public void Return(InstanceLease lease)
        {
            if (lease == null || !lease.OwnedBy(this)) throw new InvalidOperationException("实例已归还或不属于该资源系统。");
            lease.Dispose();
        }
        internal void ReturnInternal(InstanceLease lease, GameObject instance)
        {
            if (!instances.Remove(lease)) throw new InvalidOperationException("未登记的池实例。");
            var pool = pools[lease.Key];
            pool.Active--;
            try
            {
                if (instance != null)
                {
                    instance.SetActive(false);
                    foreach (var component in instance.GetComponentsInChildren<MonoBehaviour>(true))
                        if (component is IPoolable reusable) reusable.OnReturn();
                    if (!stopping && pool.Idle.Count < pool.Capacity)
                    {
                        instance.transform.SetParent(poolRoot.transform, false);
                        pool.Idle.Push(instance);
                        instance = null;
                    }
                }
            }
            finally
            {
                if (instance != null) Object.Destroy(instance);
                Trim(lease.Key, pool);
            }
        }
        private void Trim(string key, Pool pool)
        {
            if (pool.Active != 0 || pool.Idle.Count != 0) return;
            pools.Remove(key);
            Track(ReleasePrefabAfterDestroy(pool));
        }
        private static async Task ReleasePrefabAfterDestroy(Pool pool)
        {
            // Destroy 在帧末生效，保留依赖直到销毁处理之后。
            await Task.Yield();
            await Task.Yield();
            if (pool.Prefab.Status == TaskStatus.RanToCompletion) pool.Prefab.Result.Dispose();
        }
        public void ClearIdle()
        {
            foreach (var pair in new List<KeyValuePair<string, Pool>>(pools))
            {
                while (pair.Value.Idle.Count != 0) Object.Destroy(pair.Value.Idle.Pop());
                Trim(pair.Key, pair.Value);
            }
        }

        public Task<SceneLease> LoadSceneAsync(string key, CancellationToken cancellation = default)
        {
            CheckOpen(null, cancellation);
            var task = LoadSceneCore(key, cancellation); Track(task); return task;
        }
        private async Task<SceneLease> LoadSceneCore(string key, CancellationToken cancellation)
        {
            var handle = Addressables.LoadSceneAsync(key, LoadSceneMode.Additive, true);
            SceneLease lease = null;
            try
            {
                await handle.Task;
                if (handle.Status != AsyncOperationStatus.Succeeded) throw new InvalidOperationException("场景加载失败: " + key, handle.OperationException);
                lease = new SceneLease { Owner = this, Handle = handle };
                scenes.Add(lease);
                CheckOpen(null, cancellation);
                return lease;
            }
            catch
            {
                if (lease != null) await UnloadSceneAsync(lease);
                else if (handle.IsValid()) Addressables.Release(handle);
                throw;
            }
        }
        public Task UnloadSceneAsync(SceneLease lease)
        {
            if (lease == null) return Task.CompletedTask;
            if (lease.Unloading != null) return lease.Unloading;
            if (lease.Owner != this) throw new InvalidOperationException("场景不属于该资源系统。");
            lease.Unloading = UnloadCore(lease);
            return lease.Unloading;
        }
        private async Task UnloadCore(SceneLease lease)
        {
            // 编辑器停止播放或外部关闭启动入口时，Unity 可能已先自动卸载场景。
            if (!lease.Handle.IsValid()) { scenes.Remove(lease); lease.Owner = null; return; }
            var operation = Addressables.UnloadSceneAsync(lease.Handle, false);
            try
            {
                await operation.Task;
                if (operation.Status != AsyncOperationStatus.Succeeded) throw new InvalidOperationException("场景卸载失败", operation.OperationException);
                scenes.Remove(lease); lease.Owner = null;
            }
            finally { if (operation.IsValid()) Addressables.Release(operation); }
        }

        public override async Task ShutdownAsync()
        {
            stopping = true;
            // 等待所有在途请求完成其失败/取消清理，再销毁池和句柄。
            foreach (var task in pending.ToArray())
                try { await task; } catch (OperationCanceledException) { } catch (ObjectDisposedException) { }
                catch (Exception ex) { Debug.LogException(ex); }
            foreach (var scene in new List<SceneLease>(scenes)) await UnloadSceneAsync(scene);
            foreach (var instance in new List<InstanceLease>(instances)) instance.Dispose();
            ClearIdle();
            foreach (var task in pending.ToArray())
                if (!task.IsCompleted) await task;
            applicationResources.Dispose();
            foreach (var lease in new List<IDisposable>(assetLeases)) lease.Dispose();
            if (poolRoot != null) Object.Destroy(poolRoot);
            poolRoot = null; pending.Clear();
        }
    }
}
