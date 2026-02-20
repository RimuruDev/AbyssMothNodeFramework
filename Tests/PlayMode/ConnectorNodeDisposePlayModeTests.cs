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

        private sealed class DisposeProbeNode : ConnectorNode
        {
            public int BindCalls { get; private set; }
            public int DisposeCalls { get; private set; }

            public override void Bind(ServiceContainer registry) =>
                BindCalls++;

            protected override void DisposeInternal() =>
                DisposeCalls++;
        }
    }
}
