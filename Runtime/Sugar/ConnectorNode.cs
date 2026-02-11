using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public abstract class ConnectorNode : MonoBehaviour, ILocalConnectorNode, IConnectorOrder, IBind
    {
        [BoxGroup("Order")]
        [SerializeField, Min(-1)] private int order;

        [BoxGroup("State")]
        [SerializeField] private bool runWhenDisabled;

        public int Order => order;
        
        public bool RunWhenDisabled => runWhenDisabled;
        
        public virtual void Bind(ServiceRegistry registry) { }
    }
}