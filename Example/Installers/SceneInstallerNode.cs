using AbyssMoth;
using UnityEngine;

namespace AbyssMothNodeFramework.Example
{
    [AddComponentMenu("AbyssMoth Node Framework/Example/Installers/Scene Installer Node")]
    public sealed class SceneInstallerNode : ConnectorNode
    {
        [SerializeField] private string sceneTagKey = "Scene/Main";

        private ServiceContainer registry;
        private SceneConnector sceneConnector;
        private LocalConnector owner;

#if UNITY_EDITOR
        private void OnValidate() =>
            Order = -1000;
#endif

        public override void Bind(ServiceContainer registry)
        {
            this.registry = registry;
            owner = GetComponentInParent<LocalConnector>(includeInactive: true);

            if (owner != null && !string.IsNullOrWhiteSpace(sceneTagKey))
                registry.AddTagged(sceneTagKey, owner);
        }

        public override void Construct(ServiceContainer registry)
        {
            if (!registry.TryGet(out sceneConnector) || sceneConnector == null)
                FrameworkLogger.Warning("[Example] SceneConnector not found in registry.", this);
        }

        public override void AfterInit()
        {
            if (sceneConnector == null)
                return;

            if (!registry.TryGet(out SceneEntityIndex index) || index == null)
                return;

            FrameworkLogger.Info(
                $"[Example] SceneInstaller ready in scene '{sceneConnector.name}'. Registered entities: {index.RegisteredCount}",
                this);
        }

        protected override void DisposeInternal()
        {
            if (registry != null && !string.IsNullOrWhiteSpace(sceneTagKey))
                registry.RemoveTagged<LocalConnector>(sceneTagKey);
        }
    }
}
