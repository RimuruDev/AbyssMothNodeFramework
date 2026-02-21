using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbyssMoth.Tests.PlayMode
{
    public sealed class ConnectorNodeDisposePlayModeTests
    {
        [UnityTest]
        public IEnumerator Dispose_DoesNotCallDisposeInternal_WhenLifecycleNeverEntered()
        {
            var root = new GameObject("NeverExecutedConnector");
            root.SetActive(false);

            var connector = root.AddComponent<LocalConnector>();
            var node = root.AddComponent<DisposeProbeNode>();

            connector.CollectNodes();
            connector.Dispose();

            Assert.That(node.DisposeCalls, Is.EqualTo(0));

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Dispose_CallsDisposeInternal_AfterLifecycleEntered()
        {
            var root = new GameObject("ExecutedConnector");

            var connector = root.AddComponent<LocalConnector>();
            var node = root.AddComponent<DisposeProbeNode>();

            connector.CollectNodes();
            connector.Execute(new ServiceContainer());
            connector.Dispose();

            Assert.That(node.BindCalls, Is.EqualTo(1));
            Assert.That(node.DisposeCalls, Is.EqualTo(1));

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContextAccessors_AreAvailable_FromNodeLifecycle()
        {
            var root = new GameObject("ContextConnector");
            var sceneContextRoot = new GameObject("SceneContextRoot");

            var connector = root.AddComponent<LocalConnector>();
            var node = root.AddComponent<ContextProbeNode>();
            var sceneConnector = sceneContextRoot.AddComponent<SceneConnector>();
            var sceneIndex = new SceneEntityIndex();
            var appLifecycle = new AppLifecycleService();

            var registry = new ServiceContainer();
            registry.Add(sceneConnector);
            registry.Add(sceneIndex);
            registry.Add(appLifecycle);

            connector.CollectNodes();
            connector.Execute(registry);

            Assert.That(node.SawContainer, Is.True);
            Assert.That(node.SawOwnerConnector, Is.True);
            Assert.That(node.SawSceneConnector, Is.True);
            Assert.That(node.SawSceneIndex, Is.True);
            Assert.That(node.SawAppLifecycle, Is.True);
            Assert.That(node.SawGetServiceShortcut, Is.True);

            Object.Destroy(root);
            Object.Destroy(sceneContextRoot);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ContextAccessors_CanResolve_ServiceAddedLaterInSameExecute()
        {
            var root = new GameObject("LateBindConnector");
            var connector = root.AddComponent<LocalConnector>();
            var node = root.AddComponent<LateBindProbeNode>();

            connector.CollectNodes();
            connector.Execute(new ServiceContainer());

            Assert.That(node.WasNullBeforeAdd, Is.True);
            Assert.That(node.ResolvedAfterAdd, Is.True);

            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator GetService_Throws_WhenLifecycleContextNotAssignedYet()
        {
            var root = new GameObject("NoContextConnector");
            var node = root.AddComponent<GetServiceGuardProbeNode>();

            Assert.That(node.GetServiceBeforeLifecycleThrows(), Is.True);

            Object.Destroy(root);
            yield return null;
        }

        private sealed class DisposeProbeNode : ConnectorNode
        {
            public int BindCalls { get; private set; }
            public int DisposeCalls { get; private set; }

            public override void Bind(ServiceContainer registry) =>
                BindCalls++;

            protected override void DisposeInternal() =>
                DisposeCalls++;
        }

        private sealed class ContextProbeNode : ConnectorNode
        {
            public bool SawContainer { get; private set; }
            public bool SawOwnerConnector { get; private set; }
            public bool SawSceneConnector { get; private set; }
            public bool SawSceneIndex { get; private set; }
            public bool SawAppLifecycle { get; private set; }
            public bool SawGetServiceShortcut { get; private set; }

            public override void Bind(ServiceContainer registry)
            {
                SawContainer = ReferenceEquals(Container, registry);
                SawOwnerConnector = OwnerConnector != null;
                SawSceneConnector = SceneConnector != null;
                SawSceneIndex = SceneEntityIndex != null;
                SawAppLifecycle = AppLifecycle != null;
                SawGetServiceShortcut = ReferenceEquals(GetService<AppLifecycleService>(), AppLifecycle);
            }
        }

        private sealed class LateBindProbeNode : ConnectorNode
        {
            public bool WasNullBeforeAdd { get; private set; }
            public bool ResolvedAfterAdd { get; private set; }

            public override void Bind(ServiceContainer registry)
            {
                WasNullBeforeAdd = AppLifecycle == null;
                registry.Add(new AppLifecycleService());
            }

            public override void AfterInit()
            {
                ResolvedAfterAdd = AppLifecycle != null && TryGetService(out AppLifecycleService _);
            }
        }

        private sealed class GetServiceGuardProbeNode : ConnectorNode
        {
            public bool GetServiceBeforeLifecycleThrows()
            {
                try
                {
                    _ = GetService<AppLifecycleService>();
                    return false;
                }
                catch (System.InvalidOperationException)
                {
                    return true;
                }
            }
        }
    }
}
