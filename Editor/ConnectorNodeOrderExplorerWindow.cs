#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AbyssMoth
{
    public sealed class ConnectorNodeOrderExplorerWindow : EditorWindow
    {
        private LocalConnector connector;

        private readonly List<MonoBehaviour> nodes = new(capacity: 128);
        private ReorderableList list;

        private int baseOrder = 0;
        private int step = 1;

        private int shiftFrom = 0;
        private int shiftDelta = 1;

        [MenuItem("AbyssMoth/Tools/" + EditorMenuConstants.EmojiCode_1 + " Connector Node Order Explorer", priority = EditorMenuConstants.ConnectorNodeOrderExplorerWindow_Priority)]
        public static void Open()
        {
            var window = GetWindow<ConnectorNodeOrderExplorerWindow>();
            window.titleContent = new GUIContent("Node Order");
            window.Show();
        }

        public void OnEnable()
        {
            TryUseSelection();
            Refresh();
            BuildList();
        }

        public void OnGUI()
        {
            DrawTopBar();

            if (connector == null)
            {
                EditorGUILayout.HelpBox("Pick a LocalConnector (Selection or drag here).", MessageType.Info);
                return;
            }

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
                connector = (LocalConnector)EditorGUILayout.ObjectField(connector, typeof(LocalConnector), true);

                if (GUILayout.Button("Use Selection", GUILayout.Width(110f)))
                {
                    TryUseSelection();
                    Refresh();
                }

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

                GUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Collect Nodes (Rescan)"))
                    {
                        Undo.RecordObject(connector, "Collect Nodes");
                        connector.CollectNodes();
                        EditorUtility.SetDirty(connector);
                        Refresh();
                    }
                }
            }
        }

        private void BuildList()
        {
            list = new ReorderableList(nodes, typeof(MonoBehaviour), draggable: true, displayHeader: true, displayAddButton: false, displayRemoveButton: false);

            list.drawHeaderCallback = rect =>
            {
                EditorGUI.LabelField(rect, "Nodes in connector (drag to define order, then Apply)");
            };

            list.drawElementCallback = (rect, index, isActive, isFocused) =>
            {
                if (index < 0 || index >= nodes.Count)
                    return;

                var node = nodes[index];
                if (node == null)
                    return;

                rect.height = EditorGUIUtility.singleLineHeight;

                var orderRect = rect;
                orderRect.width = 70f;

                var objRect = rect;
                objRect.xMin += 74f;

                var order = GetOrder(node);
                EditorGUI.LabelField(orderRect, order.ToString());

                EditorGUI.ObjectField(objRect, node, typeof(MonoBehaviour), true);
            };

            list.onReorderCallback = _ => Repaint();
        }

        private void TryUseSelection()
        {
            var go = Selection.activeGameObject;
            if (go == null)
                return;

            var found = go.GetComponent<LocalConnector>();
            if (found != null)
                connector = found;
        }

        private void Refresh()
        {
            nodes.Clear();

            if (connector == null)
                return;

            var listNodes = connector.Nodes;
            if (listNodes == null)
                return;

            for (var i = 0; i < listNodes.Count; i++)
            {
                var node = listNodes[i];
                if (node != null)
                    nodes.Add(node);
            }
        }

        private void ApplyOrderFromList()
        {
            if (connector == null)
                return;

            if (step == 0)
                step = 1;

            var toRecord = new List<UnityEngine.Object>(nodes.Count + 1);
            toRecord.Add(connector);

            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                    toRecord.Add(nodes[i]);
            }

            Undo.RecordObjects(toRecord.ToArray(), "Apply Node Order");

            var value = baseOrder;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (TrySetOrder(node, value))
                    value += step;
                else
                    Debug.LogWarning($"Node has no int field 'order': {node.GetType().Name}", node);
            }

            EditorUtility.SetDirty(connector);
            EditorSceneManager.MarkAllScenesDirty();

            connector.CollectNodes();
            EditorUtility.SetDirty(connector);
        }

        private void ShiftRange()
        {
            if (connector == null)
                return;

            var toRecord = new List<UnityEngine.Object>(nodes.Count + 1);
            toRecord.Add(connector);

            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] != null)
                    toRecord.Add(nodes[i]);
            }

            Undo.RecordObjects(toRecord.ToArray(), "Shift Node Order Range");

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                var value = GetOrder(node);
                if (value >= shiftFrom)
                    TrySetOrder(node, value + shiftDelta);
            }

            EditorUtility.SetDirty(connector);
            EditorSceneManager.MarkAllScenesDirty();

            connector.CollectNodes();
            EditorUtility.SetDirty(connector);
        }

        private static int GetOrder(MonoBehaviour node)
        {
            if (node == null)
                return 0;

            var so = new SerializedObject(node);
            var prop = so.FindProperty("order");

            if (prop != null && prop.propertyType == SerializedPropertyType.Integer)
                return prop.intValue;

            return 0;
        }

        private static bool TrySetOrder(MonoBehaviour node, int value)
        {
            if (node == null)
                return false;

            var so = new SerializedObject(node);
            var prop = so.FindProperty("order");

            if (prop == null || prop.propertyType != SerializedPropertyType.Integer)
                return false;

            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(node);

            return true;
        }
    }
}
#endif
