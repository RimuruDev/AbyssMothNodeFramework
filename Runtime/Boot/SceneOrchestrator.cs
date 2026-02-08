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

        public void Awake()
        {
            projectRoot = FindFirstObjectByType<ProjectRootConnector>(FindObjectsInactive.Include);

            if (projectRoot == null)
            {
                var prefabs = Resources.LoadAll<ProjectRootConnector>(path: "");

                if (prefabs == null || prefabs.Length == 0)
                {
                    Debug.LogError("ProjectRootConnector prefab not found in Resources");
                }
                else if (prefabs.Length > 1)
                {
                    Debug.LogError($"Multiple ProjectRootConnector prefabs found in Resources: {prefabs.Length}");
                    projectRoot = Instantiate(prefabs[0]);
                }
                else
                {
                    projectRoot = Instantiate(prefabs[0]);
                }
            }

            if (projectRoot == null)
            {
                Debug.LogError("ProjectRootConnector is null. Scene initialization will not run.");
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public void OnDestroy() => 
            SceneManager.sceneLoaded -= OnSceneLoaded;

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
                        Debug.LogError($"Two SceneConnector in scene: {scene.name}", this);
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
            Debug.LogWarning($"SceneConnector not found in scene: {scene.name}");
#endif
        }
    }
}