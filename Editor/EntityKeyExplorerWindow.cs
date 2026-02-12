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
    public sealed class EntityKeyExplorerWindow : EditorWindow
    {
        private readonly List<EntityKeyBehaviour> items = new(capacity: 256);
        private ReorderableList list;

        private bool includeInactive = true;
        private string search = "";

        private int baseId = 1;
        private int step = 1;

        private int shiftFromId = 1;
        private int shiftDelta = 1;

        [MenuItem("AbyssMoth/Tools/Entity Key Explorer")]
        public static void Open()
        {
            var window = GetWindow<EntityKeyExplorerWindow>();
            window.titleContent = new GUIContent("Entity Key Explorer");
            window.Show();
        }

        public void OnEnable()
        {
            Refresh();
            BuildList();
        }

        private void BuildList()
        {
            list = new ReorderableList(items, typeof(EntityKeyBehaviour), draggable: true, displayHeader: true, displayAddButton: false, displayRemoveButton: false);

            list.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "EntityKeyBehaviour in open scenes");
            };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= items.Count)
                    return;

                var item = items[index];
                if (item == null)
                    return;

                rect.height = EditorGUIUtility.singleLineHeight;

                var left = rect;
                left.width = 60f;

                var mid = rect;
                mid.xMin += 64f;
                mid.width = 70f;

                var right = rect;
                right.xMin += 140f;

                EditorGUI.BeginChangeCheck();

                var id = GetId(item);
                var newId = EditorGUI.IntField(mid, id);

                if (EditorGUI.EndChangeCheck())
                    SetId(item, newId);

                var label = item.gameObject.name;
                EditorGUI.ObjectField(right, item, typeof(EntityKeyBehaviour), allowSceneObjects: true);

                var tag = GetTag(item);
                var tagRect = right;
                tagRect.xMin += 260f;
                tagRect.width = 140f;

                EditorGUI.BeginChangeCheck();
                var newTag = EditorGUI.TextField(tagRect, tag);
                if (EditorGUI.EndChangeCheck())
                    SetTag(item, newTag);

                var activeRect = left;
                EditorGUI.LabelField(activeRect, item.gameObject.activeInHierarchy ? "Active" : "Inactive");
            };

            list.onReorderCallback = _ => Repaint();
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
                includeInactive = EditorGUILayout.ToggleLeft("Include Inactive", includeInactive, GUILayout.Width(140f));
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
                    if (GUILayout.Button("Assign Missing"))
                        AssignMissing();

                    if (GUILayout.Button("Fix Duplicates"))
                        FixDuplicates();
                }

                GUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    baseId = EditorGUILayout.IntField("Base", baseId);
                    step = EditorGUILayout.IntField("Step", step);

                    if (GUILayout.Button("Apply Order", GUILayout.Width(120f)))
                        ApplyOrder();
                }

                GUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    shiftFromId = EditorGUILayout.IntField("Shift From Id >=", shiftFromId);
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
                    var found = roots[r].GetComponentsInChildren<EntityKeyBehaviour>(includeInactive);
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

        private bool PassSearch(EntityKeyBehaviour item)
        {
            if (string.IsNullOrEmpty(search))
                return true;

            var _name = item.gameObject.name;
            if (!string.IsNullOrWhiteSpace(_name) && _name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            var tag = GetTag(item);
            if (!string.IsNullOrEmpty(tag) && tag.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return false;
        }

        private void AssignMissing()
        {
            Undo.RecordObjects(items.ToArray(), "Assign Missing Entity Ids");

            var used = new HashSet<int>();
            for (var i = 0; i < items.Count; i++)
            {
                var id = GetId(items[i]);
                if (id > 0)
                    used.Add(id);
            }

            var next = 1;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var id = GetId(item);

                if (id > 0)
                    continue;

                while (used.Contains(next))
                    next++;

                SetId(item, next);
                used.Add(next);
                next++;
            }

            MarkDirtyAll();
        }

        private void FixDuplicates()
        {
            Undo.RecordObjects(items.ToArray(), "Fix Duplicate Entity Ids");

            var used = new HashSet<int>();
            var next = 1;

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var id = GetId(item);

                if (id <= 0)
                    continue;

                if (used.Add(id))
                    continue;

                while (used.Contains(next))
                    next++;

                SetId(item, next);
                used.Add(next);
                next++;
            }

            MarkDirtyAll();
        }

        private void ApplyOrder()
        {
            if (step == 0)
                step = 1;

            Undo.RecordObjects(items.ToArray(), "Apply Entity Id Order");

            var id = baseId;
            for (var i = 0; i < items.Count; i++)
            {
                SetId(items[i], id);
                id += step;
            }

            MarkDirtyAll();
        }

        private void ShiftRange()
        {
            Undo.RecordObjects(items.ToArray(), "Shift Entity Id Range");

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var id = GetId(item);

                if (id >= shiftFromId && id > 0)
                    SetId(item, id + shiftDelta);
            }

            MarkDirtyAll();
        }

        private void MarkDirtyAll()
        {
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item != null)
                    EditorUtility.SetDirty(item);
            }

            EditorSceneManager.MarkAllScenesDirty();
        }

        private static int GetId(EntityKeyBehaviour item)
        {
            var so = new SerializedObject(item);
            var prop = so.FindProperty("id");
            return prop != null ? prop.intValue : 0;
        }

        private static void SetId(EntityKeyBehaviour item, int value)
        {
            if (item == null)
                return;

            var so = new SerializedObject(item);
            var prop = so.FindProperty("id");
            if (prop == null)
                return;

            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static string GetTag(EntityKeyBehaviour item)
        {
            var so = new SerializedObject(item);
            var prop = so.FindProperty("entityTag");
            return prop != null ? prop.stringValue : "";
        }

        private static void SetTag(EntityKeyBehaviour item, string value)
        {
            if (item == null)
                return;

            var so = new SerializedObject(item);
            var prop = so.FindProperty("entityTag");
            if (prop == null)
                return;

            prop.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif