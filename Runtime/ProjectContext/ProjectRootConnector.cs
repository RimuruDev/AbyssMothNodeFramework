using UnityEngine;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    [SelectionBase]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1100)]
    [RequireComponent(typeof(LocalConnector))]
    public sealed class ProjectRootConnector : MonoBehaviour
    {
        private LocalConnector connector;

        public ServiceRegistry ProjectContext { get; private set; }

        public void Awake()
        {
            ProjectContext = new ServiceRegistry();

            connector = GetComponent<LocalConnector>();
            connector.Execute(ProjectContext);

            transform.SetParent(p: null);
            DontDestroyOnLoad(gameObject);
        }
    }
}