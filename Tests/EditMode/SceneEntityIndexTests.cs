using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AbyssMoth.Tests.EditMode
{
    public class SceneEntityIndexTests
    {
        private readonly List<GameObject> roots = new(capacity: 16);

        [TearDown]
        public void TearDown()
        {
            for (var i = roots.Count - 1; i >= 0; i--)
            {
                var root = roots[i];
                if (root == null)
                    continue;

                Object.DestroyImmediate(root);
            }

            roots.Clear();
        }

        [Test]
        public void Register_AssignsRuntimeId_AndSupportsLookupByIdTagConnectorAndNode()
        {
            var connector = CreateConnector(
                name: "Hero",
                tag: "Hero",
                forcedId: 0,
                withBaseNode: true,
                withDerivedNode: true);

            var index = new SceneEntityIndex();
            index.Register(connector);

            Assert.That(connector.EntityId, Is.GreaterThan(0));
            Assert.That(index.TryGetById(connector.EntityId, out var byId), Is.True);
            Assert.That(byId, Is.SameAs(connector));

            Assert.That(index.TryGetFirstByTag("Hero", out var byTag), Is.True);
            Assert.That(byTag, Is.SameAs(connector));

            Assert.That(index.TryGetFirstConnector<LocalConnector>(out var byType), Is.True);
            Assert.That(byType, Is.SameAs(connector));

            Assert.That(index.TryGetFirstNode<ConnectorNode>(out var node), Is.True);
            Assert.That(node, Is.Not.Null);
        }

        [Test]
        public void Register_ReassignsDuplicateManualId()
        {
            var first = CreateConnector(name: "EnemyA", tag: "Enemy", forcedId: 7);
            var second = CreateConnector(name: "EnemyB", tag: "Enemy", forcedId: 7);

            var index = new SceneEntityIndex();
            index.Register(first);
            index.Register(second);

            Assert.That(first.EntityId, Is.EqualTo(7));
            Assert.That(second.EntityId, Is.Not.EqualTo(7));
            Assert.That(second.EntityId, Is.GreaterThan(0));

            Assert.That(index.TryGetById(7, out var byId), Is.True);
            Assert.That(byId, Is.SameAs(first));
        }

        [Test]
        public void Refresh_UpdatesTagIndex()
        {
            var connector = CreateConnector(name: "Boss", tag: "Enemy");

            var index = new SceneEntityIndex();
            index.Register(connector);

            Assert.That(index.TryGetFirstByTag("Enemy", out var enemy), Is.True);
            Assert.That(enemy, Is.SameAs(connector));

            connector.SetEntityTag("Boss");
            index.Refresh(connector);

            Assert.That(index.TryGetFirstByTag("Enemy", out _), Is.False);
            Assert.That(index.TryGetFirstByTag("Boss", out var boss), Is.True);
            Assert.That(boss, Is.SameAs(connector));
        }

        [Test]
        public void Refresh_RemovesTag_WhenTagCleared()
        {
            var connector = CreateConnector(name: "Enemy", tag: "Enemy");

            var index = new SceneEntityIndex();
            index.Register(connector);

            Assert.That(index.TryGetFirstByTag("Enemy", out var enemy), Is.True);
            Assert.That(enemy, Is.SameAs(connector));

            connector.SetEntityTag(string.Empty);
            index.Refresh(connector);

            Assert.That(index.TryGetFirstByTag("Enemy", out _), Is.False);
            Assert.That(index.GetAllByTag("Enemy").Count, Is.EqualTo(0));
        }

        [Test]
        public void GetNodes_IncludeDerivedSwitch_WorksAsExpected()
        {
            var connector = CreateConnector(
                name: "MixedNodes",
                tag: "Test",
                withBaseNode: true,
                withDerivedNode: true);

            var index = new SceneEntityIndex();
            index.Register(connector);

            var buffer = new List<TestBaseNode>();

            var withDerived = index.GetNodes(buffer, includeDerived: true);
            Assert.That(withDerived, Is.EqualTo(2));

            var exactOnly = index.GetNodes(buffer, includeDerived: false);
            Assert.That(exactOnly, Is.EqualTo(1));
            Assert.That(buffer[0].GetType(), Is.EqualTo(typeof(TestBaseNode)));
        }

        [Test]
        public void Unregister_RemovesConnectorFromAllIndexes()
        {
            var connector = CreateConnector(name: "Npc", tag: "Npc", withBaseNode: true);

            var index = new SceneEntityIndex();
            index.Register(connector);
            index.Unregister(connector);

            Assert.That(index.RegisteredCount, Is.EqualTo(0));
            Assert.That(index.TryGetById(connector.EntityId, out _), Is.False);
            Assert.That(index.TryGetFirstByTag("Npc", out _), Is.False);
            Assert.That(index.TryGetFirstConnector<LocalConnector>(out _), Is.False);
            Assert.That(index.TryGetFirstNode<TestBaseNode>(out _), Is.False);
        }

        private LocalConnector CreateConnector(
            string name,
            string tag = null,
            int forcedId = 0,
            bool withBaseNode = false,
            bool withDerivedNode = false)
        {
            var root = new GameObject(name);
            roots.Add(root);

            var connector = root.AddComponent<LocalConnector>();

            if (forcedId > 0)
                SetEntityIdViaReflection(connector, forcedId);

            if (!string.IsNullOrWhiteSpace(tag))
                connector.SetEntityTag(tag);

            if (withBaseNode)
                root.AddComponent<TestBaseNode>();

            if (withDerivedNode)
                root.AddComponent<TestDerivedNode>();

            connector.CollectNodes();
            return connector;
        }

        private static void SetEntityIdViaReflection(LocalConnector connector, int id)
        {
            var field = typeof(LocalConnector).GetField(
                "entityId",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.That(field, Is.Not.Null, "Failed to access LocalConnector.entityId via reflection.");
            field.SetValue(connector, id);
        }

        private class TestBaseNode : ConnectorNode { }
        private sealed class TestDerivedNode : TestBaseNode { }
    }
}
