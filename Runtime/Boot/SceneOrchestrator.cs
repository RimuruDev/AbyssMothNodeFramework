using System;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.SceneManagement;

namespace AbyssMoth
{
    [Preserve]
    [DefaultExecutionOrder(-1000)]
    public sealed class SceneOrchestrator : MonoBehaviour
    {
        private ProjectRootConnector projectRoot;
        private AppLifecycleService lifecycle;
        private FrameworkConfig config;

        public void Awake()
        {
            config = FrameworkConfig.TryLoadDefault();
            FrameworkLogger.Configure(config);
            FrameworkLogger.Boot("SceneOrchestrator.Awake()", this);

            projectRoot = FindFirstObjectByType<ProjectRootConnector>(FindObjectsInactive.Include);

            // === Project Root === //
            {
                if (projectRoot == null)
                {
                    var prefab = Resources.Load<ProjectRootConnector>(path: NodeFrameworkPaths.ProjectRootConnector);

                    if (prefab == null)
                        prefab = Resources.Load<ProjectRootConnector>(path: NodeFrameworkPaths.LegacyProjectRootConnector);

                    if (prefab != null)
                    {
                        projectRoot = Instantiate(prefab);
                    }
                    else
                    {
                        var go = new GameObject(name: nameof(ProjectRootConnector));
                        projectRoot = go.AddComponent<ProjectRootConnector>();
                    }
                }

                if (projectRoot == null)
                {
                    FrameworkLogger.Error("ProjectRootConnector is null. Scene initialization will not run.", this);
                    return;
                }
            }

            // === App Lifecycle === //
            {
                if (projectRoot != null)
                {
                    if (config != null && !projectRoot.ProjectContext.Contains<FrameworkConfig>())
                        projectRoot.ProjectContext.Add(config);

                    if (!projectRoot.ProjectContext.TryGet(out lifecycle))
                    {
                        lifecycle = new AppLifecycleService();
                        projectRoot.ProjectContext.Add(lifecycle);
                        FrameworkLogger.Boot("AppLifecycleService registered", this);
                    }
                }
            }

            // === App SceneTransition === //
            {
                if (config != null &&
                    config.RegisterDefaultSceneTransitionService &&
                    !projectRoot.ProjectContext.TryGet(out SceneTransitionService transitions))
                {
                    var transitionSceneName = config.DefaultTransitionSceneName;

                    if (string.IsNullOrWhiteSpace(transitionSceneName))
                        transitionSceneName = Constants.EmptySceneTransitionName;

                    transitions = new SceneTransitionService(runner: this, transitionSceneName);
                    projectRoot.ProjectContext.Add(transitions);
                    FrameworkLogger.Boot($"SceneTransitionService registered ({transitionSceneName})", this);
                }
            }


            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnApplicationFocus(bool hasFocus) =>
            lifecycle?.RaiseFocusChanged(hasFocus, sender: this);

        private void OnApplicationPause(bool pauseStatus) =>
            lifecycle?.RaisePauseChanged(pauseStatus, sender: this);

        public void OnDestroy() =>
            SceneManager.sceneLoaded -= OnSceneLoaded;

        private void OnApplicationQuit() =>
            lifecycle?.RaiseQuit(sender: this);

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FrameworkLogger.Boot($"Scene loaded: {scene.name} ({mode})", this);
            FrameworkInitializationTrace.BeginSession($"Scene:{scene.name} ({mode})", this);

            try
            {
                FrameworkInitializationTrace.Event($"SceneOrchestrator.OnSceneLoaded: {scene.name} ({mode})", this);

                if (SceneConnectorRegistry.TryGet(scene, out var sceneConnector) && sceneConnector != null)
                {
                    sceneConnector.Execute(projectRoot.ProjectContext);
                    return;
                }

                var all = FindObjectsByType<SceneConnector>(FindObjectsInactive.Include, FindObjectsSortMode.None);

                SceneConnector found = null;

                for (var i = 0; i < all.Length; i++)
                {
                    var item = all[i];

                    if (item != null && item.gameObject.scene == scene)
                    {
                        if (found != null)
                        {
                            FrameworkLogger.Error($"Two SceneConnector in scene: {scene.name}", this);
                            return;
                        }

                        found = item;
                    }
                }

                if (found != null)
                {
                    found.Execute(projectRoot.ProjectContext);
                    return;
                }

#if UNITY_EDITOR
                if (string.Equals(scene.name, Constants.EmptySceneTransitionName))
                    return;

                FrameworkLogger.Warning($"SceneConnector not found in scene: {scene.name}", this);
#endif
            }
            finally
            {
                FrameworkInitializationTrace.EndSession(this);
            }
        }
    }
}
