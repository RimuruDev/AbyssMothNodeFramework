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

        private SceneConnector spawnSceneConnector;
        private SceneEntityIndex spawnSceneEntityIndex;

        public override void Construct(ServiceContainer registry)
        {
            spawnSceneConnector = registry.Get<SceneConnector>();
            spawnSceneEntityIndex = registry.Get<SceneEntityIndex>();
        }

        public override void Init()
        {
            if (!spawnOnInit || prefab == null)
                return;

            if (spawnSceneConnector == null || spawnSceneEntityIndex == null)
                return;

            var spawned = spawnSceneConnector.InstantiateAndRegister(prefab);
            if (spawned == null)
                return;

            if (!string.IsNullOrWhiteSpace(spawnedTag))
                spawned.SetEntityTag(spawnedTag);

            if (spawnSceneEntityIndex.TryGetFirstByTag(spawnedTag, out var found))
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
