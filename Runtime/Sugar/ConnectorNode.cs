using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public abstract class ConnectorNode : MonoBehaviour, ILocalConnectorNode, IConnectorOrder
    {
        [BoxGroup("Order")]
        [SerializeField, Min(0)] private int order;

        [BoxGroup("State")]
        [SerializeField] private bool runWhenDisabled;

        public int Order => order;
        
        public bool RunWhenDisabled => runWhenDisabled;
    }
}