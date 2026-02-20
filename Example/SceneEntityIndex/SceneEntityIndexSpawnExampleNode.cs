using AbyssMoth;
using UnityEngine;

namespace AbyssMothNodeFramework.Example
{
    [AddComponentMenu("AbyssMoth Node Framework/Example/Scene Entity Index Spawn Node")]
    public sealed class SceneEntityIndexSpawnExampleNode : ConnectorNode
    {
        [SerializeField] private LocalConnector prefab;
        [SerializeField] private string spawnedTag = "SpawnedEntity";
        [SerializeField] private bool spawnOnInit = true;

        private SceneConnector sceneConnector;
        private SceneEntityIndex sceneEntityIndex;

        public override void Construct(ServiceContainer registry)
        {
            sceneConnector = registry.Get<SceneConnector>();
            sceneEntityIndex = registry.Get<SceneEntityIndex>();

            Debug.Log($"=>sceneConnector-> {sceneConnector==null}");
        }

        public override void Init()
        {
            if (!spawnOnInit || prefab == null)
                return;

            if (sceneConnector == null || sceneEntityIndex == null)
                return;

            var spawned = sceneConnector.InstantiateAndRegister(prefab);
            if (spawned == null)
                return;

            if (!string.IsNullOrWhiteSpace(spawnedTag))
                spawned.SetEntityTag(spawnedTag);

            if (sceneEntityIndex.TryGetFirstByTag(spawnedTag, out var found))
                FrameworkLogger.Info(
                    $"[Example] Instant spawn lookup by tag '{spawnedTag}' -> {found.name}",
                    this);
            else
                FrameworkLogger.Warning(
                    $"[Example] Spawned connector was not found by tag '{spawnedTag}'.",
                    this);
        }
    }
}
