#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssMoth
{
    public static class EntityIdEditorUtils
    {
        private const string PrefKey = "AbyssMoth.EntityId.Next";

        private static readonly HashSet<int> used = new();

        public static int AllocateUniqueId()
        {
            CollectUsedIds();

            var next = EditorPrefs.GetInt(PrefKey, 1);
            if (next < 1)
                next = 1;

            while (used.Contains(next))
                next++;

            EditorPrefs.SetInt(PrefKey, next + 1);
            used.Add(next);

            return next;
        }

        private static void CollectUsedIds()
        {
            used.Clear();

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                var roots = scene.GetRootGameObjects();
                for (var r = 0; r < roots.Length; r++)
                {
                    var keys = roots[r].GetComponentsInChildren<EntityKeyBehaviour>(true);
                    for (var k = 0; k < keys.Length; k++)
                    {
                        var id = keys[k] != null ? keys[k].Id : 0;
                        if (id > 0)
                            used.Add(id);
                    }
                }
            }

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage == null || stage.prefabContentsRoot == null)
                return;

            var stageKeys = stage.prefabContentsRoot.GetComponentsInChildren<EntityKeyBehaviour>(true);
            for (var i = 0; i < stageKeys.Length; i++)
            {
                var id = stageKeys[i] != null ? stageKeys[i].Id : 0;
                if (id > 0)
                    used.Add(id);
            }
        }
    }
}
#endif