using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public sealed partial class SceneEntityIndex
    {
        private static readonly IReadOnlyList<LocalConnector> emptyConnectors = Array.Empty<LocalConnector>();

        private readonly Dictionary<int, LocalConnector> idMap = new(capacity: 128);

        private readonly Dictionary<string, List<LocalConnector>> tagMap = new(capacity: 64, comparer: StringComparer.Ordinal);

        private readonly Dictionary<Type, List<MonoBehaviour>> nodeMap = new(capacity: 256);

        private readonly HashSet<LocalConnector> registered = new(ReferenceComparer<LocalConnector>.Instance);

        private int nextRuntimeId = 1;
        
        public int RegisteredCount => registered.Count;
        public int IdCount => idMap.Count;
        public int TagCount => tagMap.Count;
        public int NodeTypeCount => nodeMap.Count;

        public int AllocateId()
        {
            if (nextRuntimeId < 1)
                nextRuntimeId = 1;

            while (idMap.ContainsKey(nextRuntimeId))
                nextRuntimeId++;

            var id = nextRuntimeId;
            nextRuntimeId++;
            return id;
        }
        
        public void Clear()
        {
            idMap.Clear();
            tagMap.Clear();
            nodeMap.Clear();
            registered.Clear();
        }

        public void Register(LocalConnector connector)
        {
            if (connector == null)
                return;

            if (!registered.Add(connector))
                return;

            RegisterEntityKey(connector);
            RegisterNodes(connector);
        }

        public void Unregister(LocalConnector connector)
        {
            if (connector == null)
                return;

            if (!registered.Remove(connector))
                return;

            UnregisterEntityKey(connector);
            UnregisterNodes(connector);
        }

        public bool TryGetById(int id, out LocalConnector connector)
        {
            if (id <= 0)
            {
                connector = null;
                return false;
            }

            if (idMap.TryGetValue(id, out connector))
                return connector != null;

            return false;
        }

        public bool TryGetFirstByTag(string tag, out LocalConnector connector)
        {
            if (string.IsNullOrEmpty(tag))
            {
                connector = null;
                return false;
            }

            if (!tagMap.TryGetValue(tag, out var list) || list == null || list.Count == 0)
            {
                connector = null;
                return false;
            }

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item != null)
                {
                    connector = item;
                    return true;
                }
            }

            connector = null;
            return false;
        }

        public IReadOnlyList<LocalConnector> GetAllByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return emptyConnectors;

            if (tagMap.TryGetValue(tag, out var list) && list != null)
                return list;

            return emptyConnectors;
        }

        public bool TryGetFirstNode<T>(out T node, bool includeDerived = true) where T : class
        {
            node = null;

            if (nodeMap.TryGetValue(typeof(T), out var list) && list != null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is T typed)
                    {
                        node = typed;
                        return true;
                    }
                }
            }

            if (!includeDerived)
                return false;

            foreach (var kv in nodeMap)
            {
                if (!typeof(T).IsAssignableFrom(kv.Key))
                    continue;

                var nodes = kv.Value;
                if (nodes == null)
                    continue;

                for (var i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] is T typed)
                    {
                        node = typed;
                        return true;
                    }
                }
            }

            return false;
        }
        
        public int GetNodes<T>(List<T> buffer, bool includeDerived = true) where T : class
        {
            if (buffer == null)
                return 0;

            buffer.Clear();

            if (nodeMap.TryGetValue(typeof(T), out var list) && list != null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is T typed)
                        buffer.Add(typed);
                }
            }

            if (!includeDerived)
                return buffer.Count;

            foreach (var kv in nodeMap)
            {
                if (kv.Key == typeof(T))
                    continue;

                if (!typeof(T).IsAssignableFrom(kv.Key))
                    continue;

                var nodes = kv.Value;
                if (nodes == null)
                    continue;

                for (var i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] is T typed)
                        buffer.Add(typed);
                }
            }

            return buffer.Count;
        }
        
        public bool TryGetNodeInConnector<T>(LocalConnector connector, out T node) where T : MonoBehaviour
        {
            node = null;

            if (connector == null)
                return false;

            var nodes = connector.Nodes;
            if (nodes == null)
                return false;

            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is T typed)
                {
                    node = typed;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetNodeInFirstByTag<T>(string tag, out T node) where T : MonoBehaviour
        {
            node = null;

            if (!TryGetFirstByTag(tag, out var connector))
                return false;

            return TryGetNodeInConnector(connector, out node);
        }

        private void RegisterEntityKey(LocalConnector connector)
        {
            var key = connector.GetComponent<EntityKeyBehaviour>();
            if (key == null)
                return;

            if (key.Id > 0)
            {
                if (idMap.TryGetValue(key.Id, out var existing) && existing != null && existing != connector)
                    Debug.LogError($"SceneEntityIndex: Duplicate Id {key.Id} on {connector.name} and {existing.name}",
                        connector);
                else
                    idMap[key.Id] = connector;
            }

            if (!string.IsNullOrEmpty(key.Tag))
            {
                if (!tagMap.TryGetValue(key.Tag, out var list) || list == null)
                {
                    list = new List<LocalConnector>(capacity: 4);
                    tagMap[key.Tag] = list;
                }

                list.Add(connector);
            }
        }

        private void UnregisterEntityKey(LocalConnector connector)
        {
            var key = connector.GetComponent<EntityKeyBehaviour>();
            if (key == null)
                return;

            if (key.Id > 0 && idMap.TryGetValue(key.Id, out var existing) && existing == connector)
                idMap.Remove(key.Id);

            if (!string.IsNullOrEmpty(key.Tag) && tagMap.TryGetValue(key.Tag, out var list) && list != null)
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (ReferenceEquals(list[i], connector))
                    {
                        list.RemoveAt(i);
                        break;
                    }
                }

                if (list.Count == 0)
                    tagMap.Remove(key.Tag);
            }
        }

        private void RegisterNodes(LocalConnector connector)
        {
            var nodes = connector.Nodes;
            if (nodes == null)
                return;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                var type = node.GetType();

                if (!nodeMap.TryGetValue(type, out var list) || list == null)
                {
                    list = new List<MonoBehaviour>(capacity: 4);
                    nodeMap[type] = list;
                }

                list.Add(node);
            }
        }

        private void UnregisterNodes(LocalConnector connector)
        {
            var nodes = connector.Nodes;
            if (nodes == null)
                return;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                var type = node.GetType();

                if (!nodeMap.TryGetValue(type, out var list) || list == null)
                    continue;

                list.Remove(node);

                if (list.Count == 0)
                    nodeMap.Remove(type);
            }
        }

        public string BuildDump(int maxItemsPerGroup = 40)
        {
            var sb = new System.Text.StringBuilder(2048);

            sb.AppendLine("SceneEntityIndex");
            sb.AppendLine($"Registered: {registered.Count}");
            sb.AppendLine($"Ids: {idMap.Count}");
            sb.AppendLine($"Tags: {tagMap.Count}");
            sb.AppendLine($"NodeTypes: {nodeMap.Count}");
            sb.AppendLine();

            if (idMap.Count > 0)
            {
                sb.AppendLine("IdMap:");
                var ids = new List<int>(idMap.Count);
                foreach (var kv in idMap)
                    ids.Add(kv.Key);

                ids.Sort();

                var limit = Mathf.Min(ids.Count, maxItemsPerGroup);
                for (var i = 0; i < limit; i++)
                {
                    var id = ids[i];
                    var c = idMap[id];
                    sb.AppendLine($"  {id} -> {(c != null ? c.name : "null")}");
                }

                if (ids.Count > limit)
                    sb.AppendLine($"  ... +{ids.Count - limit} more");

                sb.AppendLine();
            }

            if (tagMap.Count > 0)
            {
                sb.AppendLine("TagMap:");
                var tags = new List<string>(tagMap.Count);
                foreach (var kv in tagMap)
                    tags.Add(kv.Key);

                tags.Sort(StringComparer.Ordinal);

                var limit = Mathf.Min(tags.Count, maxItemsPerGroup);
                for (var i = 0; i < limit; i++)
                {
                    var tag = tags[i];
                    var list = tagMap[tag];

                    sb.Append($"  {tag} [{(list != null ? list.Count : 0)}]: ");

                    if (list != null)
                    {
                        var innerLimit = Mathf.Min(list.Count, 12);
                        for (var j = 0; j < innerLimit; j++)
                        {
                            var c = list[j];
                            sb.Append(c != null ? c.name : "null");

                            if (j < innerLimit - 1)
                                sb.Append(", ");
                        }

                        if (list.Count > innerLimit)
                            sb.Append(" ...");
                    }

                    sb.AppendLine();
                }

                if (tags.Count > limit)
                    sb.AppendLine($"  ... +{tags.Count - limit} more");

                sb.AppendLine();
            }

            if (nodeMap.Count > 0)
            {
                sb.AppendLine("NodeTypes:");
                var types = new List<Type>(nodeMap.Count);
                foreach (var kv in nodeMap)
                    types.Add(kv.Key);

                types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

                var limit = Mathf.Min(types.Count, maxItemsPerGroup);
                for (var i = 0; i < limit; i++)
                {
                    var t = types[i];
                    var list = nodeMap[t];
                    sb.AppendLine($"  {t.Name} -> {(list != null ? list.Count : 0)}");
                }

                if (types.Count > limit)
                    sb.AppendLine($"  ... +{types.Count - limit} more");
            }

            return sb.ToString();
        }

        public override string ToString() => BuildDump();
    }
    
    // === Unsafe API === //
    public sealed partial class SceneEntityIndex
    {
        public T FindFirstNode<T>(bool includeDerived = true) where T : class
        {
            if (nodeMap.TryGetValue(typeof(T), out var list) && list != null)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is T typed) return typed;
                }
            }

            if (!includeDerived) return null;
            foreach (var kv in nodeMap)
            {
                if (!typeof(T).IsAssignableFrom(kv.Key)) continue;
                var nodes = kv.Value;
                if (nodes == null) continue;
                for (var i = 0; i < nodes.Count; i++)
                {
                    if (nodes[i] is T typed) return typed;
                }
            }

            return null;
        }

        public T GetFirstNodeOrThrow<T>(bool includeDerived = true) where T : class
        {
            if (TryGetFirstNode<T>(out var node, includeDerived))
                return node;

            throw new InvalidOperationException($"SceneEntityIndex: Node {typeof(T).Name} not found.\n{BuildDump()}");
        }
        
        public LocalConnector GetFirstByTagOrThrow(string tag)
        {
            if (TryGetFirstByTag(tag, out var connector))
                return connector;

            throw new InvalidOperationException($"SceneEntityIndex: Tag '{tag}' not found.\n{BuildDump()}");
        }

        public LocalConnector GetByIdOrThrow(int id)
        {
            if (TryGetById(id, out var connector))
                return connector;

            throw new InvalidOperationException($"SceneEntityIndex: Id '{id}' not found.\n{BuildDump()}");
        }
    }
}