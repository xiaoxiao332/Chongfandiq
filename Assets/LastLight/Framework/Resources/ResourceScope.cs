using System;
using System.Collections.Generic;

namespace LastLight
{
    /// <summary>拥有资源租约；关闭后拒绝新租约，迟到结果由加载器释放。</summary>
    public sealed class ResourceScope : IDisposable
    {
        private readonly HashSet<IDisposable> leases = new HashSet<IDisposable>();
        public bool IsClosed { get; private set; }
        public int Count => leases.Count;
        internal void Track(IDisposable lease)
        {
            if (IsClosed) { lease.Dispose(); throw new ObjectDisposedException(nameof(ResourceScope)); }
            leases.Add(lease);
        }
        internal void Forget(IDisposable lease) => leases.Remove(lease);
        public void Dispose()
        {
            if (IsClosed) return;
            IsClosed = true;
            List<Exception> errors = null;
            foreach (var lease in new List<IDisposable>(leases))
                try { lease.Dispose(); } catch (Exception ex) { (errors ??= new List<Exception>()).Add(ex); }
            leases.Clear();
            if (errors != null) throw new AggregateException(errors);
        }
    }

    public sealed class ResourceLease<T> : IDisposable where T : UnityEngine.Object
    {
        private Action release;
        private readonly ResourceScope scope;
        private T asset;
        public T Asset => release != null ? asset : throw new ObjectDisposedException(nameof(ResourceLease<T>));
        internal ResourceLease(T asset, ResourceScope scope, Action release)
        { this.asset = asset; this.scope = scope; this.release = release; }
        public void Dispose()
        {
            var action = release;
            if (action == null) return;
            release = null; asset = null; scope?.Forget(this); action();
        }
    }
}
