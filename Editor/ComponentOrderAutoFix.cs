#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace AbyssMoth
{
    [InitializeOnLoad]
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    public static class ComponentOrderAutoFix
    {
        private static readonly HashSet<EntityId> queued = new();
        private static readonly List<EntityId> buffer = new(capacity: 64);

        private static bool scheduled;
        private static double nextSelectionFixTime;

        static ComponentOrderAutoFix()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
            EditorApplication.update += Update;
        }

        private static void OnComponentAdded(Component component)
        {
            if (component == null)
                return;

            if (component is not EntityKeyBehaviour &&
                component is not LocalConnector &&
                component is not SceneConnector &&
                component is not ConnectorNode)
                return;

            Queue(component.gameObject);
        }

        private static void Update()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            var now = EditorApplication.timeSinceStartup;
            if (now < nextSelectionFixTime)
                return;

            nextSelectionFixTime = now + 0.25;

            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
                return;

            for (var i = 0; i < selection.Length; i++)
                Queue(selection[i]);
        }

        private static void Queue(GameObject go)
        {
            if (go == null)
                return;

            var id = go.GetEntityId();
            if (!id.IsValid())
                return;

            queued.Add(id);

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
                    ComponentOrderTools.FixOnGameObject(obj, FixOnGameObjectMode.MoveComponentEnsureBefore);
            }

            buffer.Clear();
        }
    }
}
#endif