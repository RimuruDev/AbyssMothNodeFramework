using AbyssMoth;
using UnityEngine;

namespace AbyssMothNodeFramework.Example
{
    [AddComponentMenu("AbyssMoth Node Framework/Example/Installers/Project Installer Node")]
    public sealed class ProjectInstallerNode : ConnectorNode
    {
        [SerializeField] private float defaultMoveSpeed = 5f;
        [SerializeField] private int defaultLives = 3;

        private ServiceContainer registry;
        private AppLifecycleService lifecycle;
        private ProjectInstallerConfig installedConfig;

#if UNITY_EDITOR
        private void OnValidate() =>
            Order = -1000;
#endif

        public override void Bind(ServiceContainer registry)
        {
            this.registry = registry;

            installedConfig = new ProjectInstallerConfig(defaultMoveSpeed, defaultLives);
            registry.Add(installedConfig);

            FrameworkLogger.Info("[Example] ProjectInstallerNode registered ProjectInstallerConfig", this);
        }

        public override void Construct(ServiceContainer registry)
        {
            if (!registry.TryGet(out lifecycle) || lifecycle == null)
            {
                FrameworkLogger.Warning("[Example] AppLifecycleService not found in registry.", this);
                return;
            }

            lifecycle.PauseChanged += OnPauseChanged;
        }

        protected override void DisposeInternal()
        {
            if (lifecycle != null)
                lifecycle.PauseChanged -= OnPauseChanged;

            if (registry != null && installedConfig != null)
                registry.RemoveIfSame(installedConfig);
        }

        private void OnPauseChanged(bool paused, Object sender)
        {
            FrameworkLogger.Info($"[Example] App pause changed: {paused}", this);
        }

        private sealed class ProjectInstallerConfig
        {
            public readonly float MoveSpeed;
            public readonly int Lives;

            public ProjectInstallerConfig(float moveSpeed, int lives)
            {
                MoveSpeed = moveSpeed;
                Lives = lives;
            }
        }
    }
}
