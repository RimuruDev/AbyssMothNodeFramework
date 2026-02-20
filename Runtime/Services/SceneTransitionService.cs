using System;
using UnityEngine;
using System.Collections;
using UnityEngine.Scripting;
using UnityEngine.SceneManagement;

namespace AbyssMoth
{
    [Preserve]
    public sealed class SceneTransitionService
    {
        private readonly MonoBehaviour runner;
        private readonly string transitionSceneName;
        private Coroutine activeTransition;

        public event Action<string> TransitionSceneLoaded;
        public event Action<string> TargetSceneLoaded;

        public bool IsTransitionRunning => activeTransition != null;

        public SceneTransitionService(MonoBehaviour runner, string transitionSceneName)
        {
            this.runner = runner;
            this.transitionSceneName = transitionSceneName;
        }

        public void Go(string targetSceneName, bool doCleanup = false) =>
            Go(targetSceneName, onTransitionSceneLoaded: null, onTargetSceneLoaded: null, doCleanup);

        public void Go(
            string targetSceneName,
            Action onTransitionSceneLoaded,
            Action onTargetSceneLoaded = null,
            bool doCleanup = false)
        {
            if (runner == null)
                throw new InvalidOperationException("SceneTransitionService: runner is null.");

            if (string.IsNullOrWhiteSpace(targetSceneName))
                throw new ArgumentException("Target scene name cannot be null or empty.", nameof(targetSceneName));

            if (activeTransition != null)
                throw new InvalidOperationException("SceneTransitionService: transition already running.");

            activeTransition = runner.StartCoroutine(
                GoCoroutine(targetSceneName, onTransitionSceneLoaded, onTargetSceneLoaded, doCleanup));
        }

        private IEnumerator GoCoroutine(
            string targetSceneName,
            Action onTransitionSceneLoaded,
            Action onTargetSceneLoaded,
            bool doCleanup)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(transitionSceneName))
                    yield return SceneManager.LoadSceneAsync(transitionSceneName, LoadSceneMode.Single);

                onTransitionSceneLoaded?.Invoke();
                TransitionSceneLoaded?.Invoke(transitionSceneName);

                if (doCleanup)
                {
                    yield return Resources.UnloadUnusedAssets();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                }

                yield return SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
                onTargetSceneLoaded?.Invoke();
                TargetSceneLoaded?.Invoke(targetSceneName);
            }
            finally
            {
                activeTransition = null;
            }
        }
    }
}
