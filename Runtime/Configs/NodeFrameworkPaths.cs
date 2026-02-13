using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public static class NodeFrameworkPaths
    {
        public const string ResourcesRoot = "AbyssMothNodeFramework";

        public const string ProjectRootConnector = ResourcesRoot + "/ProjectRootConnector";
        public const string ConnectorDebugConfig = ResourcesRoot + "/ConnectorDebugConfig";

        public const string LegacyProjectRootConnector = "ProjectRootConnector";
        public const string LegacyConnectorDebugConfig = "ConnectorDebugConfig";
    }
}