using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace LastLight
{
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField] private PanelCatalog catalog;
        [SerializeField] private RectTransform[] layers;
        private sealed class OpenEntry
        {
            public PanelDefinition Definition;
            public CancellationTokenSource Cancellation;
            public Task<PanelBase> Loading;
            public InstanceLease Lease;
            public PanelBase Panel;
            public IDisposable Pause;
        }
        private readonly Dictionary<string, OpenEntry> entries = new Dictionary<string, OpenEntry>();
        private readonly List<OpenEntry> stack = new List<OpenEntry>();
        private readonly List<Task> pending = new List<Task>();
        private readonly ResourceScope resources = new ResourceScope();
        private ResourceSystem loader;
        private PauseSystem pause;
        private bool stopping;
        public int OpenCount => stack.Count;
        public bool HasModal { get { foreach (var entry in stack) if (entry.Definition.Modal) return true; return false; } }
        public RectTransform LayerRoot(UILayer layer) => layers[(int)layer];
        public void Configure(PanelCatalog definitions, RectTransform[] layerRoots) { catalog = definitions; layers = layerRoots; }
        public void Initialize(ResourceSystem loader, PauseSystem pause)
        {
            if (catalog == null || layers == null || layers.Length != 4) throw new InvalidOperationException("UIManager 缺少面板目录或四层根节点。");
            var ids = new HashSet<string>();
            foreach (var definition in catalog.Panels)
                if (string.IsNullOrWhiteSpace(definition.Id) || !ids.Add(definition.Id) || string.IsNullOrWhiteSpace(definition.Address))
                    throw new InvalidOperationException("无效或重复面板配置: " + definition.Id);
            foreach (var layer in layers) if (layer == null) throw new InvalidOperationException("UI 层级根节点缺失。");
            this.loader = loader; this.pause = pause;
        }
        public Task<PanelBase> OpenAsync(string id, object context = null)
        {
            if (stopping || loader == null) throw new InvalidOperationException("UIManager 未启动或已关闭。");
            if (entries.TryGetValue(id, out var existing)) return existing.Loading;
            var entry = new OpenEntry { Definition = catalog.Find(id), Cancellation = new CancellationTokenSource() };
            entries.Add(id, entry);
            entry.Loading = Load(entry, context);
            pending.RemoveAll(t => t.IsCompleted); pending.Add(entry.Loading);
            return entry.Loading;
        }
        private async Task<PanelBase> Load(OpenEntry entry, object context)
        {
            await Task.Yield();
            try
            {
                entry.Lease = await loader.RentAsync(entry.Definition.Address, layers[(int)entry.Definition.Layer], resources,
                    entry.Definition.Cache ? 1 : 0, entry.Cancellation.Token);
                entry.Cancellation.Token.ThrowIfCancellationRequested();
                entry.Panel = entry.Lease.Instance.GetComponent<PanelBase>();
                if (entry.Panel == null) throw new InvalidOperationException("面板 Prefab 缺少 PanelBase: " + entry.Definition.Address);
                // Prefab 根节点预先全屏拉伸；这里仅归零位置，不修改锚点与内部布局。
                ((RectTransform)entry.Panel.transform).anchoredPosition3D = Vector3.zero;
                entry.Panel.transform.SetAsLastSibling();
                entry.Panel.InitializePanel();
                if (entry.Definition.Pause) entry.Pause = pause.Acquire();
                stack.Add(entry);
                entry.Panel.OpenPanel(context);
                entry.Cancellation.Token.ThrowIfCancellationRequested();
                RefreshFocus();
                return entry.Panel;
            }
            catch
            {
                if (entries.TryGetValue(entry.Definition.Id, out var current) && current == entry) entries.Remove(entry.Definition.Id);
                Cleanup(entry);
                throw;
            }
            finally { entry.Cancellation.Dispose(); entry.Cancellation = null; }
        }
        public void Close(string id)
        {
            if (!entries.TryGetValue(id, out var entry)) return;
            entries.Remove(id);
            entry.Cancellation?.Cancel();
            // 在途请求由 Load 的 catch 统一回收；完成的面板立即回收。
            if (entry.Loading.IsCompleted) Cleanup(entry);
        }
        private void Cleanup(OpenEntry entry)
        {
            stack.Remove(entry);
            try
            {
                if (entry.Panel != null) { entry.Panel.ClosePanel(); entry.Panel.RecyclePanel(); }
            }
            finally
            {
                entry.Pause?.Dispose(); entry.Pause = null;
                entry.Lease?.Dispose(); entry.Lease = null; entry.Panel = null;
                RefreshFocus();
            }
        }
        public bool CloseTop()
        {
            var ordered = Ordered();
            for (int i = ordered.Count - 1; i >= 0; --i)
            {
                var definition = ordered[i].Definition;
                if (definition.Closable) { Close(definition.Id); return true; }
                if (definition.Modal) return false;
            }
            return false;
        }
        private List<OpenEntry> Ordered()
        {
            var ordered = new List<OpenEntry>(stack);
            ordered.Sort((a, b) =>
            {
                int layer = a.Definition.Layer.CompareTo(b.Definition.Layer);
                return layer != 0 ? layer : stack.IndexOf(a).CompareTo(stack.IndexOf(b));
            });
            return ordered;
        }
        private void RefreshFocus()
        {
            var ordered = Ordered(); bool blocked = false;
            for (int i = ordered.Count - 1; i >= 0; --i)
            {
                ordered[i].Panel.SetFocus(!blocked);
                if (ordered[i].Definition.Modal) blocked = true;
            }
        }
        public void ClearScope(SystemLifetime lifetime)
        {
            foreach (var pair in new List<KeyValuePair<string, OpenEntry>>(entries))
                if (pair.Value.Definition.Lifetime >= lifetime) Close(pair.Key);
        }
        public async Task ShutdownAsync()
        {
            stopping = true; ClearScope(SystemLifetime.Application);
            foreach (var task in pending)
                try { await task; } catch (OperationCanceledException) { } catch (Exception ex) { Debug.LogException(ex); }
            resources.Dispose(); pending.Clear(); loader = null; pause = null;
        }
    }
}
