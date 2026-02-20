using AbyssMoth;
using UnityEngine;

namespace AbyssMothNodeFramework.Example
{
    [AddComponentMenu("AbyssMoth Node Framework/Example/Lifecycle/Lifecycle Probe Node")]
    public sealed class LifecycleProbeNode : ConnectorNode
    {
        [SerializeField] private bool logTicks;

        public override void Bind(ServiceContainer registry) =>
            FrameworkLogger.Info("[Example] LifecycleProbe.Bind", this);

        public override void Construct(ServiceContainer registry) =>
            FrameworkLogger.Info("[Example] LifecycleProbe.Construct", this);

        public override void BeforeInit() =>
            FrameworkLogger.Info("[Example] LifecycleProbe.BeforeInit", this);

        public override void Init() =>
            FrameworkLogger.Info("[Example] LifecycleProbe.Init", this);

        public override void AfterInit() =>
            FrameworkLogger.Info("[Example] LifecycleProbe.AfterInit", this);

        public override void Tick(float deltaTime)
        {
            if (!logTicks)
                return;

            FrameworkLogger.Verbose($"[Example] LifecycleProbe.Tick dt={deltaTime:0.000}", this);
        }

        protected override void DisposeInternal() =>
            FrameworkLogger.Info("[Example] LifecycleProbe.DisposeInternal", this);
    }
}
