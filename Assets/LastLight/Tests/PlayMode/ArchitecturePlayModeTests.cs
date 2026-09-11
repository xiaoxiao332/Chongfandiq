using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace LastLight.Tests
{
    public sealed class ArchitecturePlayModeTests
    {
        [UnityTearDown]
        public IEnumerator CleanupPersistentRoot()
        {
            foreach(var root in Object.FindObjectsByType<GlobalManager>(FindObjectsSortMode.None))
            {
                var shutdown=root.ShutdownAsync();while(!shutdown.IsCompleted)yield return null;
                if(shutdown.IsFaulted)throw shutdown.Exception;
                Object.Destroy(root.gameObject);
            }
            var boot=Object.FindFirstObjectByType<ArchitectureBootstrap>();if(boot!=null)Object.Destroy(boot.gameObject);
            yield return null;
        }
        [UnityTest]
        public IEnumerator CompleteArchitectureLifecycle()
        {
#if UNITY_EDITOR
            var loading = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/LastLight/ArchitectureSample/Scenes/Bootstrap.unity", new LoadSceneParameters(LoadSceneMode.Single));
#else
            var loading = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
#endif
            yield return loading;
            var boot = Object.FindFirstObjectByType<ArchitectureBootstrap>(); Assert.IsNotNull(boot);
            var test = ArchitectureSmoke.RunAsync(boot);
            float deadline = Time.realtimeSinceStartup + 180;
            while (!test.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(test.IsCompleted, "Architecture smoke timed out.");
            if (test.IsFaulted) Assert.Fail(test.Exception.ToString());
            Assert.IsFalse(test.IsCanceled);
        }
    }
}
