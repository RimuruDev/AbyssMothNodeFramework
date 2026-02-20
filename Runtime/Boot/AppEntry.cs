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
            var config = FrameworkConfig.TryLoadDefault();
            FrameworkLogger.Configure(config);
            FrameworkLogger.Boot("AppEntry.OnBeforeSceneLoad()");

            ApplyBootstrapSettings(config);

            var existing = Object.FindFirstObjectByType<SceneOrchestrator>(FindObjectsInactive.Include);

            if (existing != null)
                return;

            var globalRoot = new GameObject(name: "GlobalRoot");
            globalRoot.AddComponent<SceneOrchestrator>();
            Object.DontDestroyOnLoad(globalRoot);
            FrameworkLogger.Boot("SceneOrchestrator instantiated from AppEntry", globalRoot);
        }

        private static void ApplyBootstrapSettings(FrameworkConfig config)
        {
            if (config == null || !config.ApplyBootstrapSettings)
                return;

            if (config.OverrideTargetFrameRate)
                Application.targetFrameRate = config.TargetFrameRate;

            if (config.OverrideVSyncCount)
                QualitySettings.vSyncCount = config.VSyncCount;

            var sleepTimeout = config.ResolveSleepTimeoutValue();
            if (sleepTimeout != int.MinValue)
                Screen.sleepTimeout = sleepTimeout;
        }
    }
}
