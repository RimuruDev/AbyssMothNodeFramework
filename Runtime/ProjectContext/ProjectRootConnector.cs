#if UNITY_EDITOR
#define UNITY_EDITOR_MODE
#endif

using UnityEngine;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    [SelectionBase]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1100)]
    public sealed class ProjectRootConnector : LocalConnector
    {
        public ServiceContainer ProjectContext { get; private set; }

#if UNITY_EDITOR_MODE
        public override void OnValidate()
        {
            Order = -1;
            base.OnValidate();
        }
#endif

        private void Awake()
        {
            ProjectRootRegistry.Set(root: this);
            
            ProjectContext = new ServiceContainer();

            var config = FrameworkConfig.TryLoadDefault();
            if (config != null)
            {
                FrameworkLogger.Configure(config);

                if (!ProjectContext.Contains<FrameworkConfig>())
                    ProjectContext.Add(config);
            }

            FrameworkLogger.Boot("ProjectRootConnector.Execute()", this);
            Execute(ProjectContext, sender: this);

            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        public override void OnDestroy()
        {
            if (!Application.isPlaying)
                return;

            ProjectRootRegistry.Clear(root: this);
        }
    }
}
