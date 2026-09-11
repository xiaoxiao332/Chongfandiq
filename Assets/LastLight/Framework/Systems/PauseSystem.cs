using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace LastLight
{
    public sealed class PauseSystem : GameSystemBase
    {
        private readonly HashSet<IDisposable> tokens = new HashSet<IDisposable>();
        private float resumeScale;
        public bool Paused => tokens.Count != 0;
        public IDisposable Acquire()
        {
            if (!Paused) { resumeScale = Time.timeScale; Time.timeScale = 0; }
            var token = new ReleaseAction(t =>
            {
                if (tokens.Remove(t) && !Paused) Time.timeScale = resumeScale;
            });
            tokens.Add(token);
            return token;
        }
        public override Task ShutdownAsync()
        {
            foreach (var token in new List<IDisposable>(tokens)) token.Dispose();
            return Task.CompletedTask;
        }
    }

    internal sealed class ReleaseAction : IDisposable
    {
        private Action<IDisposable> release;
        public ReleaseAction(Action<IDisposable> release) { this.release = release; }
        public void Dispose() { var action = release; release = null; action?.Invoke(this); }
    }
}
