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
        public ServiceRegistry ProjectContext { get; private set; }

        public override void OnValidate()
        {
            Order = -1;
            base.OnValidate();
        }

        private void Awake()
        {
            ProjectContext = new ServiceRegistry();

            Debug.Log($"<color=magenta> <color=red>></color> ProjectRootConnector.Execute()</color>");
            Execute(ProjectContext, sender: this);

            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }
    }
}