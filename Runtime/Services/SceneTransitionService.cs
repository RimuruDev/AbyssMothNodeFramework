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

        public SceneTransitionService(MonoBehaviour runner, string transitionSceneName)
        {
            this.runner = runner;
            this.transitionSceneName = transitionSceneName;
        }

        public void Go(string targetSceneName, bool doCleanup = false)
        {
            if (runner == null)
                throw new InvalidOperationException("SceneTransitionService: runner is null.");

            runner.StartCoroutine(GoCoroutine(targetSceneName, doCleanup));
        }

        private IEnumerator GoCoroutine(string targetSceneName, bool doCleanup)
        {
            yield return SceneManager.LoadSceneAsync(transitionSceneName, LoadSceneMode.Single);

            if (doCleanup)
            {
                yield return Resources.UnloadUnusedAssets();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }

            yield return SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Single);
        }
    }
}