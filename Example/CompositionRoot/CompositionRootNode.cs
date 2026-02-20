using AbyssMoth;
using System.Diagnostics.CodeAnalysis;

namespace AbyssMothNodeFramework.Example
{
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    public sealed class CompositionRootNode : ConnectorNode
    {
        private ServiceContainer registry;
        private SceneEntityIndex sceneEntityIndex;

#if UNITY_EDITOR
        private void OnValidate()
        {
            Order = -1000;

            var hasParentLocal = transform.parent != null &&
                                 transform.parent.GetComponentInParent<LocalConnector>() != null;

            if (!hasParentLocal && GetComponent<LocalConnector>() == null)
                gameObject.AddComponent<LocalConnector>();
        }
#endif

        public override void Bind(ServiceContainer registry)
        {
            this.registry = registry;

            if (!registry.Contains<SceneBootstrapMarker>())
                registry.Add(new SceneBootstrapMarker(sceneName: gameObject.scene.name));
        }

        public override void Construct(ServiceContainer registry)
        {
            sceneEntityIndex = registry.Get<SceneEntityIndex>();
        }

        public override void Init()
        {
            FrameworkLogger.Info("[Example] CompositionRootNode.Init()", this);
        }

        public override void AfterInit()
        {
            if (sceneEntityIndex.TryGetFirstByTag("Hero", out var hero))
                FrameworkLogger.Info($"[Example] Hero found: {hero.name}", this);
        }

        protected override void DisposeInternal()
        {
            if (registry != null)
                registry.Remove<SceneBootstrapMarker>();
        }

        private sealed class SceneBootstrapMarker
        {
            public readonly string SceneName;

            public SceneBootstrapMarker(string sceneName) =>
                SceneName = sceneName;
        }
    }
}
