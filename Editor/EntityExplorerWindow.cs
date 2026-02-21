#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AbyssMoth
{
    public sealed class EntityExplorerWindow : EditorWindow
    {
        private readonly List<LocalConnector> items = new(capacity: 256);
        private ReorderableList list;

        private bool includeInactive = true;
        private string search = "";

        [MenuItem("AbyssMoth/Tools/" + EditorMenuConstants.WindowCode_2 + " Entity Explorer", priority = EditorMenuConstants.EntityExplorerWindow_Priority)]
        public static void Open()
        {
            var window = GetWindow<EntityExplorerWindow>();
            window.titleContent = new GUIContent("Entity Explorer");
            window.Show();
        }

        public void OnEnable()
        {
            Refresh();
            BuildList();
        }

        public void OnGUI()
        {
            DrawTopBar();

            if (list == null)
                BuildList();

            list.DoLayoutList();

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "EntityId is assigned automatically in LocalConnector. Edit only EntityTag.",
                MessageType.Info);
        }

        private void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                includeInactive = EditorGUILayout.ToggleLeft("Include Inactive", includeInactive, GUILayout.Width(140f));
                search = EditorGUILayout.TextField(search);

                if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
                {
                    Refresh();
                    Repaint();
                }
            }
        }

        private void BuildList()
        {
            list = new ReorderableList(items, typeof(LocalConnector), draggable: false, displayHeader: true,
                displayAddButton: false, displayRemoveButton: false);

            list.drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "LocalConnector Entity Data"); };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= items.Count)
                    return;

                var item = items[index];
                if (item == null)
                    return;

                rect.height = EditorGUIUtility.singleLineHeight;

                var stateRect = rect;
                stateRect.width = 55f;

                var idRect = rect;
                idRect.xMin += 60f;
                idRect.width = 70f;

                var objRect = rect;
                objRect.xMin += 136f;
                objRect.width = 220f;

                var tagRect = rect;
                tagRect.xMin = objRect.xMax + 8f;

                EditorGUI.LabelField(stateRect, item.gameObject.activeInHierarchy ? "Active" : "Off");
                EditorGUI.LabelField(idRect, item.EntityId.ToString());
                EditorGUI.ObjectField(objRect, item, typeof(LocalConnector), allowSceneObjects: true);

                EditorGUI.BeginChangeCheck();
                var newTag = EditorGUI.TextField(tagRect, item.EntityTag ?? "");
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(item, "Set LocalConnector Entity Tag");
                    item.SetEntityTag(newTag);
                    EditorUtility.SetDirty(item);

                    if (item.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(item.gameObject.scene);
                }
            };
        }

        private void Refresh()
        {
            items.Clear();

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                    continue;

                var roots = scene.GetRootGameObjects();
                for (var r = 0; r < roots.Length; r++)
                {
                    var found = roots[r].GetComponentsInChildren<LocalConnector>(includeInactive);
                    for (var f = 0; f < found.Length; f++)
                    {
                        var item = found[f];
                        if (item == null)
                            continue;

                        if (!PassSearch(item))
                            continue;

                        items.Add(item);
                    }
                }
            }

            items.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private bool PassSearch(LocalConnector item)
        {
            if (string.IsNullOrWhiteSpace(search))
                return true;

            var normalized = search.Trim();

            var itemName = item.gameObject.name;
            if (!string.IsNullOrWhiteSpace(itemName) &&
                itemName.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var tag = item.EntityTag;
            if (!string.IsNullOrWhiteSpace(tag) && tag.IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return item.EntityId.ToString().IndexOf(normalized, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
