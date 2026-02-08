using UnityEngine;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public static class AppEntry
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void OnBeforeSceneLoad()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var existing = Object.FindFirstObjectByType<SceneOrchestrator>(FindObjectsInactive.Include);

            if (existing != null)
                return;

            var globalRoot = new GameObject(name: "GlobalRoot");
            globalRoot.AddComponent<SceneOrchestrator>();
            Object.DontDestroyOnLoad(globalRoot);
        }
    }
}