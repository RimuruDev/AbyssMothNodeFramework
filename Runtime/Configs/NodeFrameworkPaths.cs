using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public static class NodeFrameworkPaths
    {
        public const string ResourcesRoot = "AbyssMothNodeFramework";
        public const string ProjectRootConnector = ResourcesRoot + "/ProjectRootConnector";
        public const string FrameworkConfig = ResourcesRoot + "/FrameworkConfig";

        public const string PackageResourcesRoot = "AbyssMothNodeFrameworkPackage";
        public const string PackageProjectRootConnector = PackageResourcesRoot + "/ProjectRootConnector";
        public const string PackageFrameworkConfig = PackageResourcesRoot + "/FrameworkConfig";

        public const string LegacyProjectRootConnector = "ProjectRootConnector";
        public const string LegacyFrameworkConfig = "FrameworkConfig";
    }
}
