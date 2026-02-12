#if UNITY_EDITOR
using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;

namespace AbyssMoth
{
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    public static class ComponentOrderTools
    {
        [MenuItem("AbyssMoth/Tools/Fix Component Order In Selected Prefabs")]
        public static void FixInSelectedPrefabs()
        {
            var guids = Selection.assetGUIDs;
            if (guids == null || guids.Length == 0)
                return;

            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path))
                    continue;

                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                    continue;

                FixPrefabAtPath(path);
            }
        }

        private static void FixPrefabAtPath(string path)
        {
            var root = PrefabUtility.LoadPrefabContents(path);

            try
            {
                FixInHierarchy(root);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void FixInHierarchy(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
                FixOnGameObject(transforms[i].gameObject);
        }

        public static void FixOnGameObject(GameObject go)
        {
            if (go == null)
                return;

            var localConnector = go.GetComponent<LocalConnector>();
            var entityKey = go.GetComponent<EntityKeyBehaviour>();
            var sceneConnector = go.GetComponent<SceneConnector>();

            MoveToTop(localConnector);
            MoveToTop(entityKey);
            MoveToTop(sceneConnector);
        }

        private static void MoveToTop(Component component)
        {
            if (component == null)
                return;

            while (ComponentUtility.MoveComponentUp(component)) { }
        }
    }
}
#endif