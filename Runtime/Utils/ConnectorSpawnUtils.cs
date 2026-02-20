using UnityEngine;

namespace AbyssMoth
{
    public static class ConnectorSpawnUtils
    {
        public static T InstantiateAndRegister<T>(
            this ServiceContainer registry,
            T prefab,
            Transform parent = null,
            bool worldPositionStays = false) where T : LocalConnector
        {
            if (registry == null || prefab == null)
                return null;

            if (!registry.TryGet(out SceneConnector sceneConnector) || sceneConnector == null)
                throw new System.InvalidOperationException("SceneConnector is not registered in current ServiceContainer.");

            return sceneConnector.InstantiateAndRegister(prefab, parent, worldPositionStays);
        }

        public static T InstantiateAndRegister<T>(
            this ServiceContainer registry,
            T prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null) where T : LocalConnector
        {
            if (registry == null || prefab == null)
                return null;

            if (!registry.TryGet(out SceneConnector sceneConnector) || sceneConnector == null)
                throw new System.InvalidOperationException("SceneConnector is not registered in current ServiceContainer.");

            return sceneConnector.InstantiateAndRegister(prefab, position, rotation, parent);
        }
    }
}
