using System;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public static class ProjectRootRegistry
    {
        private static ProjectRootConnector instance;

        public static bool TryGet(out ProjectRootConnector root)
        {
            root = instance;
            return root != null;
        }

        public static ProjectRootConnector Get()
        {
            if (instance == null)
                throw new InvalidOperationException("ProjectRootConnector not initialized yet.");

            return instance;
        }

        public static ServiceContainer GetContext() =>
            Get().ProjectContext;

        public static void Set(ProjectRootConnector root)
        {
            if (root == null)
                return;

            if (instance != null && instance != root)
                FrameworkLogger.Warning(
                    $"ProjectRootRegistry: Replacing ProjectRootConnector {instance.name} -> {root.name}");

            instance = root;
        }

        public static void Clear(ProjectRootConnector root)
        {
            if (instance == root)
                instance = null;
        }
    }
}
