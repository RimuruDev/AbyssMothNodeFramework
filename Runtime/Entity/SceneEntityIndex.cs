using System;
using UnityEngine;
using UnityEngine.Scripting;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace AbyssMoth
{
    [Preserve]
    [SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
    public sealed class SceneEntityIndex
    {
        private static readonly IReadOnlyList<LocalConnector> emptyConnectors = Array.Empty<LocalConnector>();

        private readonly Dictionary<int, LocalConnector> idMap = new(capacity: 128);
        private readonly Dictionary<string, List<LocalConnector>> tagMap =
            new(capacity: 64, comparer: StringComparer.Ordinal);

        private readonly Dictionary<Type, List<LocalConnector>> connectorMap = new(capacity: 64);
        private readonly Dictionary<Type, List<MonoBehaviour>> nodeMap = new(capacity: 256);
        private readonly Dictionary<LocalConnector, string> connectorTagMap =
            new(capacity: 128, comparer: ReferenceComparer<LocalConnector>.Instance);

        private readonly HashSet<LocalConnector> registered = new(ReferenceComparer<LocalConnector>.Instance);
        private readonly HashSet<int> reservedIds = new();

        private readonly Dictionary<Type, List<Type>> assignableNodeKeysCache = new();
        private readonly Dictionary<Type, List<Type>> assignableConnectorKeysCache = new();

        private int nextRuntimeId = 1;

        public int RegisteredCount => registered.Count;
        public int IdCount => idMap.Count;
        public int TagCount => tagMap.Count;
        public int ConnectorTypeCount => connectorMap.Count;
        public int NodeTypeCount => nodeMap.Count;

        public void PrimeReservedIds(IReadOnlyList<LocalConnector> connectors, int expectedSceneHandle)
        {
            reservedIds.Clear();

            var max = 0;

            if (connectors == null)
            {
                nextRuntimeId = 1;
                return;
            }

            for (var i = 0; i < connectors.Count; i++)
            {
                var connector = connectors[i];
                if (connector == null)
                    continue;

                if (connector.gameObject.scene.handle != expectedSceneHandle)
                    continue;

                var id = connector.EntityId;
                if (id <= 0)
                    continue;

                if (!reservedIds.Add(id))
                    continue;

                if (id > max)
                    max = id;
            }

            nextRuntimeId = max + 1;

            if (nextRuntimeId < 1)
                nextRuntimeId = 1;
        }

        public int AllocateId()
        {
            if (nextRuntimeId < 1)
                nextRuntimeId = 1;

            while (idMap.ContainsKey(nextRuntimeId) || reservedIds.Contains(nextRuntimeId))
                nextRuntimeId++;

            var id = nextRuntimeId;
            nextRuntimeId++;

            reservedIds.Add(id);
            return id;
        }

        public void Clear()
        {
            idMap.Clear();
            tagMap.Clear();
            connectorMap.Clear();
            nodeMap.Clear();
            connectorTagMap.Clear();
            registered.Clear();
            reservedIds.Clear();

            nextRuntimeId = 1;

            InvalidateAssignableCache();
        }

        public void Register(LocalConnector connector)
        {
            if (connector == null)
                return;

            if (!registered.Add(connector))
                return;

            RegisterEntity(connector);
            RegisterConnectorType(connector);
            RegisterNodes(connector);

            InvalidateAssignableCache();
        }

        public void Unregister(LocalConnector connector)
        {
            if (connector == null)
                return;

            if (!registered.Remove(connector))
                return;

            UnregisterEntity(connector);
            UnregisterConnectorType(connector);
            UnregisterNodes(connector);

            InvalidateAssignableCache();
        }

        public void Refresh(LocalConnector connector)
        {
            if (connector == null)
                return;

            if (!registered.Contains(connector))
                return;

            Unregister(connector);
            Register(connector);
        }

        [SuppressMessage("ReSharper", "RedundantAssignment")]
        public bool TryGetById(int id, out LocalConnector connector)
        {
            if (!idMap.TryGetValue(id, out connector))
            {
                connector = null;
                return false;
            }

            if (connector == null)
            {
                idMap.Remove(id);
                connector = null;
                return false;
            }

            return true;
        }

        public bool TryGetFirstByTag(string tag, out LocalConnector connector)
        {
            connector = null;

            if (string.IsNullOrWhiteSpace(tag))
                return false;

            tag = tag.Trim();

            if (!tagMap.TryGetValue(tag, out var list) || list == null || list.Count == 0)
                return false;

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item != null)
                {
                    connector = item;
                    return true;
                }
            }

            return false;
        }

        public bool TryGetFirstByTag<TConnector>(string tag, out TConnector connector, bool includeDerived = true)
            where TConnector : LocalConnector
        {
            connector = null;

            if (string.IsNullOrWhiteSpace(tag))
                return false;

            if (!tagMap.TryGetValue(tag.Trim(), out var list) || list == null || list.Count == 0)
                return false;

            var requestedType = typeof(TConnector);

            for (var i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null)
                    continue;

                if (item is not TConnector typed)
                    continue;

                if (!includeDerived && item.GetType() != requestedType)
                    continue;

                connector = typed;
                return true;
            }

            return false;
        }

        public IReadOnlyList<LocalConnector> GetAllByTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return emptyConnectors;

            if (!tagMap.TryGetValue(tag.Trim(), out var list) || list == null)
                return emptyConnectors;

            PruneDeadConnectors(list);
            return list;
        }

        public int GetAllByTagNonAlloc(string tag, List<LocalConnector> connectorsBuffer)
        {
            if (connectorsBuffer == null)
                throw new ArgumentNullException(nameof(connectorsBuffer));

            connectorsBuffer.Clear();

            if (string.IsNullOrWhiteSpace(tag))
                return 0;

            if (!tagMap.TryGetValue(tag.Trim(), out var list) || list == null)
                return 0;

            for (var i = 0; i < list.Count; i++)
            {
                var connector = list[i];
                if (connector != null)
                    connectorsBuffer.Add(connector);
            }

            return connectorsBuffer.Count;
        }

        public bool TryGetFirstConnector<TConnector>(out TConnector connector, bool includeDerived = true)
            where TConnector : LocalConnector
        {
            connector = null;

            var requestedType = typeof(TConnector);

            if (connectorMap.TryGetValue(requestedType, out var direct) && direct != null)
            {
                PruneDeadConnectors(direct);

                for (var i = 0; i < direct.Count; i++)
                {
                    var item = direct[i];
                    if (item is TConnector typed)
                    {
                        connector = typed;
                        return true;
                    }
                }
            }

            if (!includeDerived)
                return false;

            var keys = GetAssignableConnectorKeys(requestedType);

            for (var i = 0; i < keys.Count; i++)
            {
                var keyType = keys[i];
                if (keyType == requestedType)
                    continue;

                if (!connectorMap.TryGetValue(keyType, out var list) || list == null)
                    continue;

                PruneDeadConnectors(list);

                for (var j = 0; j < list.Count; j++)
                {
                    var item = list[j];
                    if (item is TConnector typed)
                    {
                        connector = typed;
                        return true;
                    }
                }
            }

            return false;
        }

        public int GetConnectors<TConnector>(List<TConnector> buffer, bool includeDerived = true)
            where TConnector : LocalConnector
        {
            if (buffer == null)
                return 0;

            buffer.Clear();

            var requestedType = typeof(TConnector);

            if (connectorMap.TryGetValue(requestedType, out var direct) && direct != null)
            {
                PruneDeadConnectors(direct);

                for (var i = 0; i < direct.Count; i++)
                {
                    var item = direct[i];
                    if (item is TConnector typed)
                        buffer.Add(typed);
                }
            }

            if (!includeDerived)
                return buffer.Count;

            var keys = GetAssignableConnectorKeys(requestedType);

            for (var i = 0; i < keys.Count; i++)
            {
                var keyType = keys[i];
                if (keyType == requestedType)
                    continue;

                if (!connectorMap.TryGetValue(keyType, out var list) || list == null)
                    continue;

                PruneDeadConnectors(list);

                for (var j = 0; j < list.Count; j++)
                {
                    var item = list[j];
                    if (item is TConnector typed)
                        buffer.Add(typed);
                }
            }

            return buffer.Count;
        }

        public bool TryGetFirstNode<T>(out T node, bool includeDerived = true) where T : class
        {
            node = null;

            var requestedType = typeof(T);

            if (nodeMap.TryGetValue(requestedType, out var direct) && direct != null)
            {
                PruneDeadNodes(direct);

                for (var i = 0; i < direct.Count; i++)
                {
                    var item = direct[i];
                    if (item == null)
                        continue;

                    if (item is T typed)
                    {
                        node = typed;
                        return true;
                    }
                }
            }

            if (!includeDerived)
                return false;

            var keys = GetAssignableNodeKeys(requestedType);

            for (var i = 0; i < keys.Count; i++)
            {
                var keyType = keys[i];

                if (keyType == requestedType)
                    continue;

                if (!nodeMap.TryGetValue(keyType, out var list) || list == null)
                    continue;

                PruneDeadNodes(list);

                for (var j = 0; j < list.Count; j++)
                {
                    var item = list[j];
                    if (item == null)
                        continue;

                    if (item is T typed)
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

            var requestedType = typeof(T);

            if (nodeMap.TryGetValue(requestedType, out var direct) && direct != null)
            {
                PruneDeadNodes(direct);

                for (var i = 0; i < direct.Count; i++)
                {
                    var item = direct[i];
                    if (item == null)
                        continue;

                    if (item is T typed)
                        buffer.Add(typed);
                }
            }

            if (!includeDerived)
                return buffer.Count;

            var keys = GetAssignableNodeKeys(requestedType);

            for (var i = 0; i < keys.Count; i++)
            {
                var keyType = keys[i];

                if (keyType == requestedType)
                    continue;

                if (!nodeMap.TryGetValue(keyType, out var list) || list == null)
                    continue;

                PruneDeadNodes(list);

                for (var j = 0; j < list.Count; j++)
                {
                    var item = list[j];
                    if (item == null)
                        continue;

                    if (item is T typed)
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
                var item = nodes[i];
                if (item == null)
                    continue;

                if (item is T typed)
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

        public T FindFirstNode<T>(bool includeDerived = true) where T : class
        {
            if (TryGetFirstNode<T>(out var node, includeDerived))
                return node;

            return null;
        }

        public bool TryGetByTag(string tag, out LocalConnector connector) =>
            TryGetFirstByTag(tag, out connector);

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

        public void PruneDeadReferences()
        {
            if (idMap.Count > 0)
            {
                var ids = new List<int>(idMap.Count);

                foreach (var kv in idMap)
                {
                    if (kv.Value == null)
                        ids.Add(kv.Key);
                }

                for (var i = 0; i < ids.Count; i++)
                    idMap.Remove(ids[i]);
            }

            if (tagMap.Count > 0)
            {
                var tagsToRemove = new List<string>();

                foreach (var kv in tagMap)
                {
                    var list = kv.Value;

                    if (list == null)
                    {
                        tagsToRemove.Add(kv.Key);
                        continue;
                    }

                    PruneDeadConnectors(list);

                    if (list.Count == 0)
                        tagsToRemove.Add(kv.Key);
                }

                for (var i = 0; i < tagsToRemove.Count; i++)
                    tagMap.Remove(tagsToRemove[i]);
            }

            if (connectorMap.Count > 0)
            {
                var typesToRemove = new List<Type>();

                foreach (var kv in connectorMap)
                {
                    var list = kv.Value;

                    if (list == null)
                    {
                        typesToRemove.Add(kv.Key);
                        continue;
                    }

                    PruneDeadConnectors(list);

                    if (list.Count == 0)
                        typesToRemove.Add(kv.Key);
                }

                for (var i = 0; i < typesToRemove.Count; i++)
                    connectorMap.Remove(typesToRemove[i]);
            }

            if (nodeMap.Count > 0)
            {
                var typesToRemove = new List<Type>();

                foreach (var kv in nodeMap)
                {
                    var list = kv.Value;

                    if (list == null)
                    {
                        typesToRemove.Add(kv.Key);
                        continue;
                    }

                    PruneDeadNodes(list);

                    if (list.Count == 0)
                        typesToRemove.Add(kv.Key);
                }

                for (var i = 0; i < typesToRemove.Count; i++)
                    nodeMap.Remove(typesToRemove[i]);
            }

            if (registered.Count > 0)
            {
                var dead = new List<LocalConnector>();

                foreach (var connector in registered)
                {
                    if (connector == null)
                        dead.Add(connector);
                }

                for (var i = 0; i < dead.Count; i++)
                    registered.Remove(dead[i]);
            }

            InvalidateAssignableCache();
        }

        public string BuildDump(int maxItemsPerGroup = 40)
        {
            PruneDeadReferences();

            var sb = new System.Text.StringBuilder(2048);

            sb.AppendLine("SceneEntityIndex");
            sb.AppendLine($"Registered: {registered.Count}");
            sb.AppendLine($"Ids: {idMap.Count}");
            sb.AppendLine($"Tags: {tagMap.Count}");
            sb.AppendLine($"ConnectorTypes: {connectorMap.Count}");
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

            if (connectorMap.Count > 0)
            {
                sb.AppendLine("ConnectorTypes:");

                var types = new List<Type>(connectorMap.Count);
                foreach (var kv in connectorMap)
                    types.Add(kv.Key);

                types.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));

                var limit = Mathf.Min(types.Count, maxItemsPerGroup);

                for (var i = 0; i < limit; i++)
                {
                    var t = types[i];
                    var list = connectorMap[t];
                    sb.AppendLine($"  {t.Name} -> {(list != null ? list.Count : 0)}");
                }

                if (types.Count > limit)
                    sb.AppendLine($"  ... +{types.Count - limit} more");

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

        public override string ToString() =>
            BuildDump();

        private void RegisterEntity(LocalConnector connector)
        {
            if (connector == null)
                return;

            var id = connector.EntityId;

            if (id <= 0)
            {
                id = AllocateId();
                connector.SetEntityIdInternal(id);
            }
            else
            {
                if (idMap.TryGetValue(id, out var existing) && existing != null && existing != connector)
                {
                    var previous = id;
                    id = AllocateId();
                    connector.SetEntityIdInternal(id);

                    FrameworkLogger.Warning(
                        $"SceneEntityIndex: Duplicate Id {previous}. Reassigned '{connector.name}' to {id}.",
                        connector);
                }

                reservedIds.Add(id);
            }

            if (id > 0)
                idMap[id] = connector;

            var tag = NormalizeTag(connector.EntityTag);

            if (string.IsNullOrEmpty(tag))
            {
                connectorTagMap.Remove(connector);
                return;
            }

            if (!tagMap.TryGetValue(tag, out var list) || list == null)
            {
                list = new List<LocalConnector>(capacity: 4);
                tagMap[tag] = list;
            }

            list.Add(connector);
            connectorTagMap[connector] = tag;
        }

        private void UnregisterEntity(LocalConnector connector)
        {
            if (connector == null)
                return;

            var id = connector.EntityId;
            if (id > 0 && idMap.TryGetValue(id, out var existing) && existing == connector)
                idMap.Remove(id);

            if (!connectorTagMap.TryGetValue(connector, out var tag))
                tag = NormalizeTag(connector.EntityTag);

            connectorTagMap.Remove(connector);

            if (string.IsNullOrEmpty(tag))
                return;

            if (!tagMap.TryGetValue(tag, out var list) || list == null)
                return;

            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(list[i], connector))
                {
                    list.RemoveAt(i);
                    break;
                }
            }

            if (list.Count == 0)
                tagMap.Remove(tag);
        }

        private void RegisterConnectorType(LocalConnector connector)
        {
            var type = connector.GetType();

            if (!connectorMap.TryGetValue(type, out var list) || list == null)
            {
                list = new List<LocalConnector>(capacity: 4);
                connectorMap[type] = list;
            }

            list.Add(connector);
        }

        private void UnregisterConnectorType(LocalConnector connector)
        {
            var type = connector.GetType();

            if (!connectorMap.TryGetValue(type, out var list) || list == null)
                return;

            list.Remove(connector);

            if (list.Count == 0)
                connectorMap.Remove(type);
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

                if (ReferenceEquals(node, null))
                    continue;

                var type = node.GetType();

                if (!nodeMap.TryGetValue(type, out var list) || list == null)
                    continue;

                list.Remove(node);

                if (list.Count == 0)
                    nodeMap.Remove(type);
            }
        }

        private static void PruneDeadConnectors(List<LocalConnector> list)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                    list.RemoveAt(i);
            }
        }

        private static void PruneDeadNodes(List<MonoBehaviour> list)
        {
            for (var i = list.Count - 1; i >= 0; i--)
            {
                if (list[i] == null)
                    list.RemoveAt(i);
            }
        }

        private void InvalidateAssignableCache()
        {
            assignableNodeKeysCache.Clear();
            assignableConnectorKeysCache.Clear();
        }

        private List<Type> GetAssignableNodeKeys(Type requestedType)
        {
            if (assignableNodeKeysCache.TryGetValue(requestedType, out var cached) && cached != null)
                return cached;

            var keys = new List<Type>(capacity: nodeMap.Count);

            foreach (var kv in nodeMap)
            {
                if (requestedType.IsAssignableFrom(kv.Key))
                    keys.Add(kv.Key);
            }

            assignableNodeKeysCache[requestedType] = keys;
            return keys;
        }

        private List<Type> GetAssignableConnectorKeys(Type requestedType)
        {
            if (assignableConnectorKeysCache.TryGetValue(requestedType, out var cached) && cached != null)
                return cached;

            var keys = new List<Type>(capacity: connectorMap.Count);

            foreach (var kv in connectorMap)
            {
                if (requestedType.IsAssignableFrom(kv.Key))
                    keys.Add(kv.Key);
            }

            assignableConnectorKeysCache[requestedType] = keys;
            return keys;
        }

        private static string NormalizeTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
        }
    }
}
