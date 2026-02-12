using NaughtyAttributes;
using UnityEngine;

namespace AbyssMoth
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LocalConnector))]
    public sealed class EntityKeyBehaviour : MonoBehaviour
    {
        [BoxGroup("Entity")]
        [SerializeField] private int id;

        [BoxGroup("Entity")]
        [SerializeField] private string entityTag;

        [BoxGroup("Entity")]
        [SerializeField] private bool autoAssignId = true;

        [BoxGroup("Cache")]
        [SerializeField, ReadOnly] private LocalConnector localConnector;

        public int Id => id;
        public string Tag => entityTag;
        public LocalConnector LocalConnector => localConnector;

        public void SetId(int value) => id = value;
        public void SetTag(string value) => entityTag = value;

        private void Reset() => OnValidate();

        public void OnValidate()
        {
            if (localConnector == null)
                localConnector = GetComponent<LocalConnector>();
        }

        public void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!autoAssignId)
                return;

            if (id > 0)
                return;

            if (!SceneConnectorRegistry.TryGet(gameObject.scene, out var sceneConnector))
                return;

            if (!sceneConnector.IsInitialized)
                return;

            if (!sceneConnector.SceneContext.TryGet(out SceneEntityIndex index))
                return;

            id = index.AllocateId();
        }
    }
}