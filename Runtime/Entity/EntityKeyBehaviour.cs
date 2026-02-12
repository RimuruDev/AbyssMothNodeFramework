#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
#endif

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
        [SerializeField] private string entityTag;

        [BoxGroup("Entity")]
        [SerializeField] private bool autoAssignId = true;

        [BoxGroup("Cache")]
        [SerializeField, ReadOnly] private LocalConnector localConnector;

        public int Id => id;
        public string Tag => entityTag;
        public bool AutoAssignId => autoAssignId;
        public LocalConnector LocalConnector => localConnector;

        public void SetId(int value) => id = value;
        public void SetTag(string value) => entityTag = value;

        private void Reset() => OnValidate();

        public void OnValidate()
        {
            if (localConnector == null)
                localConnector = GetComponent<LocalConnector>();
        }

#if UNITY_EDITOR
        [Button("Assign Unique Id")]
        private void AssignUniqueId()
        {
            if (Application.isPlaying)
                return;

            Undo.RecordObject(this, "Assign Unique Entity Id");

            var newId = EntityIdEditorUtils.AllocateUniqueId();
            id = newId;

            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkAllScenesDirty();
        }
#endif
    }
}