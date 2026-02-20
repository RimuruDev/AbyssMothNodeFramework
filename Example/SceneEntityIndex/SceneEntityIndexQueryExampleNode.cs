using System.Collections.Generic;
using AbyssMoth;
using UnityEngine;

namespace AbyssMothNodeFramework.Example
{
    [AddComponentMenu("AbyssMoth Node Framework/Example/Scene Entity Index Query Node")]
    public sealed class SceneEntityIndexQueryExampleNode : ConnectorNode
    {
        [SerializeField] private string tagToQuery = "Hero";
        [SerializeField] private bool logOnInit = true;

        private SceneEntityIndex sceneEntityIndex;
        private readonly List<LocalConnector> tagBuffer = new(capacity: 16);

        public override void Construct(ServiceContainer registry)
        {
            sceneEntityIndex = registry.Get<SceneEntityIndex>();
        }

        public override void Init()
        {
            if (!logOnInit || sceneEntityIndex == null)
                return;

            if (sceneEntityIndex.TryGetFirstByTag(tagToQuery, out var connector))
                FrameworkLogger.Info($"[Example] TryGetFirstByTag('{tagToQuery}') -> {connector.name}", this);
            else
                FrameworkLogger.Warning($"[Example] Tag '{tagToQuery}' not found.", this);

            if (sceneEntityIndex.TryGetNodeInFirstByTag<ConnectorNode>(tagToQuery, out var node))
                FrameworkLogger.Info($"[Example] TryGetNodeInFirstByTag -> {node.GetType().Name}", this);

            var count = sceneEntityIndex.GetAllByTagNonAlloc(tagToQuery, tagBuffer);
            FrameworkLogger.Info($"[Example] GetAllByTagNonAlloc('{tagToQuery}') -> {count}", this);
        }
    }
}
