using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AbyssMoth.Tests.PlayMode
{
    public sealed class SceneEntityIndexPlayModeTests
    {
        [UnityTest]
        public IEnumerator RuntimeSetEntityTag_RefreshesSceneIndexImmediately()
        {
            var sceneConnectorGo = new GameObject("SceneConnectorRoot");
            var sceneConnector = sceneConnectorGo.AddComponent<SceneConnector>();
            sceneConnector.CollectConnectors();
            sceneConnector.Execute(new ServiceContainer());

            Assert.That(sceneConnector.SceneContext.TryGet(out SceneEntityIndex index), Is.True);
            Assert.That(index, Is.Not.Null);

            var enemyGo = new GameObject("Enemy");
            var enemy = enemyGo.AddComponent<LocalConnector>();
            enemy.SetEntityTag("Enemy");

            sceneConnector.RegisterAndExecute(enemy);

            Assert.That(index.TryGetFirstByTag("Enemy", out var byEnemyTag), Is.True);
            Assert.That(byEnemyTag, Is.SameAs(enemy));

            enemy.SetEntityTag("Boss");

            Assert.That(index.TryGetFirstByTag("Enemy", out _), Is.False);
            Assert.That(index.TryGetFirstByTag("Boss", out var byBossTag), Is.True);
            Assert.That(byBossTag, Is.SameAs(enemy));

            Object.Destroy(enemyGo);
            Object.Destroy(sceneConnectorGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InstantiateAndRegister_AllowsImmediateLookupByTag()
        {
            var sceneConnectorGo = new GameObject("SceneConnectorRoot");
            var sceneConnector = sceneConnectorGo.AddComponent<SceneConnector>();
            sceneConnector.CollectConnectors();
            sceneConnector.Execute(new ServiceContainer());

            Assert.That(sceneConnector.SceneContext.TryGet(out SceneEntityIndex index), Is.True);
            Assert.That(index, Is.Not.Null);

            var prefabGo = new GameObject("HeroPrefab");
            prefabGo.SetActive(false);
            var prefab = prefabGo.AddComponent<LocalConnector>();
            prefab.SetEntityTag("Hero");

            var spawned = sceneConnector.InstantiateAndRegister(prefab);

            Assert.That(spawned, Is.Not.Null);
            Assert.That(index.TryGetFirstByTag("Hero", out var found), Is.True);
            Assert.That(found, Is.SameAs(spawned));

            Object.Destroy(prefabGo);
            Object.Destroy(spawned.gameObject);
            Object.Destroy(sceneConnectorGo);
            yield return null;
        }
    }
}
