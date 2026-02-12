#if UNITY_EDITOR
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;

namespace AbyssMoth
{
    [InitializeOnLoad]
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    public static class ComponentOrderAutoFix
    {
        private static readonly HashSet<int> queued = new();
        private static readonly List<int> buffer = new(capacity: 64);
        private static bool scheduled;

        static ComponentOrderAutoFix()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }

        private static void OnComponentAdded(Component component)
        {
            if (component == null)
                return;

            if (component is not EntityKeyBehaviour &&
                component is not LocalConnector &&
                component is not SceneConnector)
                return;

            Queue(component.gameObject);
        }

        private static void Queue(GameObject go)
        {
            if (go == null)
                return;

            queued.Add(go.GetInstanceID());

            if (scheduled)
                return;

            scheduled = true;
            EditorApplication.delayCall += Apply;
        }

        private static void Apply()
        {
            scheduled = false;

            buffer.Clear();
            foreach (var id in queued)
                buffer.Add(id);

            queued.Clear();

            for (var i = 0; i < buffer.Count; i++)
            {
                var obj = EditorUtility.EntityIdToObject(buffer[i]) as GameObject;
                if (obj != null)
                    ComponentOrderTools.FixOnGameObject(obj);
            }

            buffer.Clear();
        }
    }
}
#endif