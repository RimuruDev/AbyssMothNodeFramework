using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace AbyssMoth
{
    public static class SceneConnectorRegistry
    {
        private static readonly Dictionary<int, SceneConnector> map = new(capacity: 16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => map.Clear();

        public static bool TryGet(Scene scene, out SceneConnector connector)
        {
            if (map.TryGetValue(scene.handle, out connector))
                return connector != null;

            connector = null;
            
            return false;
        }

        public static void TryRegister(SceneConnector connector)
        {
            var scene = connector.gameObject.scene;

            if (map.TryGetValue(scene.handle, out var existing) && existing != null && existing != connector)
            {
                Debug.LogError($"Two SceneConnector in scene: {scene.name}", connector);
                connector.enabled = false;
                return;
            }

            map[scene.handle] = connector;
        }

        public static void Unregister(SceneConnector connector)
        {
            var scene = connector.gameObject.scene;

            if (map.TryGetValue(scene.handle, out var existing) && existing == connector)
                map.Remove(scene.handle);
        }
    }
}