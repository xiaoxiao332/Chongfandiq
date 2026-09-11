using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace LastLight.Tests
{
    public sealed class SystemRegistryTests
    {
        private sealed class First : GameSystemBase
        {
            private readonly List<string> log;
            public First(List<string> log) { this.log = log; }
            public override Task InitializeAsync(SystemRegistry systems, CancellationToken cancellation) { log.Add("start first"); return Task.CompletedTask; }
            public override Task ShutdownAsync() { log.Add("stop first"); return Task.CompletedTask; }
        }
        private sealed class Second : GameSystemBase
        {
            private readonly List<string> log;
            private readonly bool fail;
            public Second(List<string> log, bool fail = false) { this.log = log; this.fail = fail; }
            public override IReadOnlyList<Type> Dependencies => new[] { typeof(First) };
            public override Task InitializeAsync(SystemRegistry systems, CancellationToken cancellation)
            { log.Add("start second"); if (fail) throw new InvalidOperationException("initialization failure"); return Task.CompletedTask; }
            public override Task ShutdownAsync() { log.Add("stop second"); return Task.CompletedTask; }
        }
        private sealed class CycleA : GameSystemBase { public override IReadOnlyList<Type> Dependencies => new[] { typeof(CycleB) }; }
        private sealed class CycleB : GameSystemBase { public override IReadOnlyList<Type> Dependencies => new[] { typeof(CycleA) }; }
        [Test]
        public async Task StartsDependenciesBeforeConsumersAndStopsInReverse()
        {
            var log = new List<string>(); var registry = new SystemRegistry(SystemLifetime.Application);
            registry.Register(new Second(log)); registry.Register(new First(log));
            await registry.InitializeAsync(default); Assert.IsTrue(registry.Ready);
            await registry.ShutdownAsync(); await registry.ShutdownAsync();
            CollectionAssert.AreEqual(new[] { "start first", "start second", "stop second", "stop first" }, log);
        }
        [Test]
        public void MissingDependencyReportsConsumerAndDependency()
        {
            var registry = new SystemRegistry(SystemLifetime.Application); registry.Register(new Second(new List<string>()));
            var error = Assert.Throws<InvalidOperationException>(() => registry.InitializationOrder());
            StringAssert.Contains(nameof(First), error.Message); StringAssert.Contains(nameof(Second), error.Message);
        }
        [Test]
        public void CycleReportsPath()
        {
            var registry = new SystemRegistry(SystemLifetime.Application); registry.Register(new CycleA()); registry.Register(new CycleB());
            var error = Assert.Throws<InvalidOperationException>(() => registry.InitializationOrder());
            StringAssert.Contains("CycleA", error.Message); StringAssert.Contains("CycleB", error.Message);
        }
        [Test]
        public void DuplicateRegistrationRejected()
        {
            var registry = new SystemRegistry(SystemLifetime.Application); registry.Register(new First(new List<string>()));
            Assert.Throws<InvalidOperationException>(() => registry.Register(new First(new List<string>())));
        }
        [Test]
        public void FailedInitializationCleansFailedSystemAndDependencies()
        {
            var log = new List<string>(); var registry = new SystemRegistry(SystemLifetime.Application);
            registry.Register(new Second(log, true)); registry.Register(new First(log));
            Assert.ThrowsAsync<InvalidOperationException>(async () => await registry.InitializeAsync(default));
            Assert.IsFalse(registry.Ready);
            CollectionAssert.AreEqual(new[] { "start first", "start second", "stop second", "stop first" }, log);
        }
        [Test]
        public async Task ChildResolvesParentWithoutOwningItsShutdown()
        {
            var log = new List<string>(); var parent = new SystemRegistry(SystemLifetime.Application); parent.Register(new First(log));
            await parent.InitializeAsync(default);
            var child = new SystemRegistry(SystemLifetime.Session, parent); child.Register(new Second(log));
            await child.InitializeAsync(default); Assert.AreSame(parent.Get<First>(), child.Get<First>());
            await child.ShutdownAsync(); Assert.IsTrue(parent.Ready);
            CollectionAssert.AreEqual(new[] { "start first", "start second", "stop second" }, log);
            await parent.ShutdownAsync();
        }
        [Test]
        public void LongLivedSystemCannotDependOnChild()
        {
            var parent = new SystemRegistry(SystemLifetime.Application); parent.Register(new Second(new List<string>()));
            var child = new SystemRegistry(SystemLifetime.Session, parent); child.Register(new First(new List<string>()));
            Assert.Throws<InvalidOperationException>(() => parent.InitializationOrder());
            Assert.Throws<ArgumentException>(() => new SystemRegistry(SystemLifetime.Application, child));
        }
        [Test]
        public void CancellationDoesNotLeaveStartedSystems()
        {
            var log = new List<string>(); var registry = new SystemRegistry(SystemLifetime.Application); registry.Register(new First(log));
            using var source = new CancellationTokenSource(); source.Cancel();
            Assert.CatchAsync<OperationCanceledException>(async () => await registry.InitializeAsync(source.Token));
            Assert.IsEmpty(log); Assert.IsFalse(registry.Ready);
        }
        [Test]
        public void PanelPrefabsAreAuthoredFullScreen()
        {
            LastLight.Editor.ArchitectureSampleBuilder.Validate();
            TestContext.Progress.WriteLine(LastLight.Editor.ArchitectureSampleBuilder.ValidateResolutionMatrix());
        }
    }
}
