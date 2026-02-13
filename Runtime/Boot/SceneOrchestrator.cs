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

        public void Awake()
        {
            projectRoot = FindFirstObjectByType<ProjectRootConnector>(FindObjectsInactive.Include);

            // === Project Root === //
            {
                if (projectRoot == null)
                {
                    var prefab = Resources.Load<ProjectRootConnector>(path: NodeFrameworkPaths.ProjectRootConnector);

                    if (prefab == null)
                        prefab = Resources.Load<ProjectRootConnector>(path: NodeFrameworkPaths.PackageProjectRootConnector);

                    if (prefab == null)
                        prefab = Resources.Load<ProjectRootConnector>(path: NodeFrameworkPaths.LegacyProjectRootConnector);

                    if (prefab == null)
                    {
                        Debug.LogError("ProjectRootConnector prefab not found in Resources");
                        return;
                    }

                    projectRoot = Instantiate(prefab);
                }


                if (projectRoot == null)
                {
                    Debug.LogError("ProjectRootConnector is null. Scene initialization will not run.");
                    return;
                }
            }

            // === App Lifecycle === //
            {
                if (projectRoot != null)
                {
                    if (!projectRoot.ProjectContext.TryGet(out lifecycle))
                    {
                        lifecycle = new AppLifecycleService();
                        projectRoot.ProjectContext.Add(lifecycle);
                    }
                }
            }

            // === App SceneTransition === //
            {
                if (!projectRoot.ProjectContext.TryGet(out SceneTransitionService transitions))
                {
                    transitions = new SceneTransitionService(runner: this, Constants.EmptySceneTransitionName);
                    projectRoot.ProjectContext.Add(transitions);
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
                        Debug.LogError($"Two SceneConnector in scene: {scene.name}", context: this);
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

            Debug.LogWarning($"SceneConnector not found in scene: {scene.name}");
#endif
        }
    }
}