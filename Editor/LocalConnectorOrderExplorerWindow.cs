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
    public sealed class LocalConnectorOrderExplorerWindow : EditorWindow
    {
        private readonly List<LocalConnector> items = new(capacity: 512);

        private ReorderableList list;

        private bool includeInactive = true;
        private string search = "";

        private int baseOrder = 0;
        private int step = 1;

        private int shiftFrom = 0;
        private int shiftDelta = 1;

        [MenuItem("AbyssMoth/Tools/" + EditorMenuConstants.EmojiCode_1 + " LocalConnector Order Explorer", priority = EditorMenuConstants.LocalConnectorOrderExplorerWindow_Priority)]
        public static void Open()
        {
            var window = GetWindow<LocalConnectorOrderExplorerWindow>();
            window.titleContent = new GUIContent("LocalConnector Order");
            window.Show();
        }

        public void OnEnable()
        {
            Refresh();
            BuildList();
        }

        private void BuildList()
        {
            list = new ReorderableList(items, typeof(LocalConnector), draggable: true, displayHeader: true,
                displayAddButton: false, displayRemoveButton: false);

            list.drawHeaderCallback = rect => { EditorGUI.LabelField(rect, "LocalConnector in open scenes"); };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= items.Count)
                    return;

                var item = items[index];
                if (item == null)
                    return;

                rect.height = EditorGUIUtility.singleLineHeight;

                var stateRect = rect;
                stateRect.width = 62f;

                var orderRect = rect;
                orderRect.xMin += 66f;
                orderRect.width = 70f;

                var objRect = rect;
                objRect.xMin += 140f;

                EditorGUI.LabelField(stateRect, item.gameObject.activeInHierarchy ? "Active" : "Inactive");

                EditorGUI.BeginChangeCheck();
                var current = GetOrder(item);
                var next = EditorGUI.IntField(orderRect, current);
                if (EditorGUI.EndChangeCheck())
                    SetOrder(item, next);

                EditorGUI.ObjectField(objRect, item, typeof(LocalConnector), true);
            };

            list.onReorderCallback = _ =>
            {
                ApplyOrderFromList();
                Repaint();
            };
        }

        public void OnGUI()
        {
            DrawTopBar();

            if (list == null)
                BuildList();

            list.DoLayoutList();

            GUILayout.Space(6);
            DrawBatchTools();
        }

        private void DrawTopBar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                includeInactive =
                    EditorGUILayout.ToggleLeft("Include Inactive", includeInactive, GUILayout.Width(140f));
                search = EditorGUILayout.TextField(search);

                if (GUILayout.Button("Refresh", GUILayout.Width(90f)))
                {
                    Refresh();
                    Repaint();
                }
            }
        }

        private void DrawBatchTools()
        {
            using (new EditorGUILayout.VerticalScope("box"))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    baseOrder = EditorGUILayout.IntField("Base", baseOrder);
                    step = EditorGUILayout.IntField("Step", step);

                    if (GUILayout.Button("Apply Order", GUILayout.Width(120f)))
                        ApplyOrderFromList();
                }

                GUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    shiftFrom = EditorGUILayout.IntField("Shift From >=", shiftFrom);
                    shiftDelta = EditorGUILayout.IntField("Delta", shiftDelta);

                    if (GUILayout.Button("Shift Range", GUILayout.Width(120f)))
                        ShiftRange();
                }
            }
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
        }

        private bool PassSearch(LocalConnector item)
        {
            if (string.IsNullOrEmpty(search))
                return true;

            var name = item.gameObject.name;
            if (!string.IsNullOrEmpty(name) && name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private void ApplyOrderFromList()
        {
            if (step == 0)
                step = 1;

            Undo.RecordObjects(items.ToArray(), "Apply LocalConnector Order");

            var value = baseOrder;

            for (var i = 0; i < items.Count; i++)
            {
                SetOrder(items[i], value);
                value += step;
            }

            MarkDirtyAll();
        }

        private void ShiftRange()
        {
            Undo.RecordObjects(items.ToArray(), "Shift LocalConnector Order Range");

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var value = GetOrder(item);

                if (value >= shiftFrom)
                    SetOrder(item, value + shiftDelta);
            }

            MarkDirtyAll();
        }

        private void MarkDirtyAll()
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] != null)
                    EditorUtility.SetDirty(items[i]);
            }

            EditorSceneManager.MarkAllScenesDirty();
        }

        private static int GetOrder(LocalConnector item)
        {
            var so = new SerializedObject(item);
            var prop = so.FindProperty("order");
            return prop != null ? prop.intValue : 0;
        }

        private static void SetOrder(LocalConnector item, int value)
        {
            if (item == null)
                return;

            var so = new SerializedObject(item);
            var prop = so.FindProperty("order");
            if (prop == null)
                return;

            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif