using UnityEngine;
using NaughtyAttributes;

namespace AbyssMoth
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LocalConnector))]
    public sealed class EntityKeyBehaviour : MonoBehaviour
    {
        [BoxGroup("Entity")]
        [SerializeField] private int id;

        [BoxGroup("Entity")]
        [SerializeField] private new string tag;

        [BoxGroup("Cache")]
        [SerializeField, ReadOnly] private LocalConnector localConnector;

        public int Id => id;
        public string Tag => tag;
        public LocalConnector LocalConnector => localConnector;

        private void Reset() => OnValidate();

        public void OnValidate()
        {
            if (localConnector == null)
                localConnector = GetComponent<LocalConnector>();
        }
    }
}