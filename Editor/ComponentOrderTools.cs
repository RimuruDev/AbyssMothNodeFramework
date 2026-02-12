#if UNITY_EDITOR
using System;
using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEngine;
using UnityEditorInternal;
using UnityEngine.SceneManagement;

namespace AbyssMoth
{
    public enum FixOnGameObjectMode
    {
        MoveComponent = 0,
        MoveComponentEnsureBefore = 1,
    }

    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    public static class ComponentOrderTools
    {
        [MenuItem("AbyssMoth/Tools/" + Constants.WindowCode_3 + " Fix Component Order In Open Scenes", secondaryPriority = 4300)]
        public static void FixInOpenScenes()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                var roots = scene.GetRootGameObjects();
                for (var r = 0; r < roots.Length; r++)
                    FixInHierarchy(roots[r]);
            }
        }

        [MenuItem("AbyssMoth/Tools/" + Constants.WindowCode_3 + "Fix Component Order In Selection", secondaryPriority = 4500)]
        public static void FixInSelection()
        {
            var selection = Selection.gameObjects;
            if (selection == null || selection.Length == 0)
                return;

            for (var i = 0; i < selection.Length; i++)
                FixInHierarchy(selection[i]);
        }

        [MenuItem("AbyssMoth/Tools/" + Constants.WindowCode_3 + " Fix Component Order In Selected Prefabs", secondaryPriority = 4800)]
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
            if (root == null)
                return;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            for (var i = 0; i < transforms.Length; i++)
                FixOnGameObject(transforms[i].gameObject, FixOnGameObjectMode.MoveComponentEnsureBefore);
        }

        public static void FixOnGameObject(GameObject go, FixOnGameObjectMode node)
        {
            if (go == null)
                return;

            switch (node)
            {
                case FixOnGameObjectMode.MoveComponent:
                    FixOnGameObject(go);
                    break;
                case FixOnGameObjectMode.MoveComponentEnsureBefore:
                    FixOnGameObjectEnsureBefore(go);
                    break;
                default:
                    FixOnGameObject(go);
                    break;
            }
        }

        private static void MoveToTop(Component component)
        {
            if (component == null)
                return;

            while (ComponentUtility.MoveComponentUp(component))
            {
            }
        }

        private static void FixOnGameObject(GameObject go)
        {
            if (go == null)
                return;

            var sceneConnector = go.GetComponent<SceneConnector>();
            var entityKey = go.GetComponent<EntityKeyBehaviour>();
            var localConnector = go.GetComponent<LocalConnector>();

            MoveToTop(localConnector);
            MoveToTop(entityKey);
            MoveToTop(sceneConnector);
        }

        private static void FixOnGameObjectEnsureBefore(GameObject go)
        {
            if (go == null)
                return;

            var sceneConnector = go.GetComponent<SceneConnector>();
            var entityKey = go.GetComponent<EntityKeyBehaviour>();
            var localConnector = go.GetComponent<LocalConnector>();

            EnsureBefore(go, entityKey, localConnector);
            EnsureBefore(go, sceneConnector, entityKey);
            EnsureBefore(go, sceneConnector, localConnector);
        }

        private static void EnsureBefore(GameObject go, Component upper, Component lower)
        {
            if (go == null || upper == null || lower == null)
                return;

            for (var i = 0; i < 64; i++)
            {
                var indexUpper = GetIndex(go, upper);
                var indexLower = GetIndex(go, lower);

                if (indexUpper < 0 || indexLower < 0)
                    return;

                if (indexUpper < indexLower)
                    return;

                if (!ComponentUtility.MoveComponentUp(upper))
                    return;
            }
        }

        private static int GetIndex(GameObject go, Component target)
        {
            var list = go.GetComponents<Component>();

            for (var i = 0; i < list.Length; i++)
            {
                if (ReferenceEquals(list[i], target))
                    return i;
            }

            return -1;
        }
    }
}
#endif