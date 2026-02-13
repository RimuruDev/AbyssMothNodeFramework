#if UNITY_EDITOR
#define UNITY_EDITOR_MODE
#endif

#if UNITY_EDITOR_MODE
using UnityEditor;
using System.Reflection;
using UnityEditor.SceneManagement;
#endif

using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Scripting;
using System.Collections.Generic;

namespace AbyssMoth
{
    [Preserve]
    [DisallowMultipleComponent]
    [AddComponentMenu("AbyssMoth Node Framework/Local Connector")]
    public class LocalConnector : MonoBehaviour, IOrder
    {
        [BoxGroup("Order")]
        [SerializeField, Min(-1)] private int order;

        public virtual int Order
        {
            get => order;
            protected set => order = value;
        }

        [BoxGroup("State")]
        [SerializeField] private bool enabledTicks = true;

        [BoxGroup("Nodes")]
        [SerializeField, ReorderableList] private List<MonoBehaviour> nodes = new();

#if UNITY_EDITOR_MODE
        [BoxGroup("Debug")]
        [SerializeField] private bool autoCollectOnValidate;

        [BoxGroup("Debug")]
        [SerializeField] private bool autoPruneMissingOnValidate = true;
#endif

        private readonly List<MonoBehaviour> collected = new(capacity: 64);

        private bool executed;
        private bool dynamicRegistered;
        private int staticSceneHandle = -1;
        private bool disposed;

        public bool EnabledTicks => enabledTicks;

        public IReadOnlyList<MonoBehaviour> Nodes => nodes;

        private readonly List<ITick> tickCache = new();
        private readonly List<MonoBehaviour> tickMonos = new();

        private readonly List<IFixedTick> fixedTickCache = new();
        private readonly List<MonoBehaviour> fixedTickMonos = new();

        private readonly List<ILateTick> lateTickCache = new();
        private readonly List<MonoBehaviour> lateTickMonos = new();

#if UNITY_EDITOR_MODE
        private ConnectorDebugConfig debugConfig;

        [ShowNativeProperty] private int NodesCount => nodes.Count;
        [ShowNativeProperty] private bool Executed => executed;
        [ShowNativeProperty] private bool DynamicRegistered => dynamicRegistered;
        [ShowNativeProperty] private bool IsStaticForCurrentScene => IsStaticFor(gameObject.scene.handle);

        [ShowNativeProperty]
        private string NodeOrder
        {
            get
            {
                var sb = new System.Text.StringBuilder();

                for (var i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];

                    if (n == null)
                    {
                        sb.AppendLine("Missing");
                        continue;
                    }

                    var nodeOrder = n is IOrder o ? o.Order : 0;
                    sb.AppendLine($"{nodeOrder} | {n.GetType().Name}");
                }

                return sb.ToString();
            }
        }

        [Button("Prune Missing Nodes")]
        private void PruneMissingNodesButton()
        {
            if (Application.isPlaying)
                return;

            Undo.RecordObject(this, "Prune Missing Nodes");

            var removed = PruneMissingNodes(rebuildCaches: false);
            if (removed <= 0)
                return;

            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkAllScenesDirty();
        }
#endif

        public void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!SceneConnectorRegistry.TryGet(gameObject.scene, out var sceneConnector))
                return;

            if (!sceneConnector.IsInitialized)
                return;

            var sceneHandle = gameObject.scene.handle;

            if (IsStaticFor(sceneHandle))
            {
                Execute(sceneConnector.SceneContext, sender: sceneConnector);
                return;
            }

            if (dynamicRegistered)
                return;

            sceneConnector.RegisterAndExecute(this);
            dynamicRegistered = true;
        }

        public void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            TryUnregisterFromScene(force: false);
        }

        public virtual void OnDestroy()
        {
            if (!Application.isPlaying)
                return;

            Dispose();
        }

        internal void MarkStatic(int sceneHandle) =>
            staticSceneHandle = sceneHandle;

        internal bool IsStaticFor(int sceneHandle) =>
            staticSceneHandle == sceneHandle && staticSceneHandle != -1;

        public void Execute(ServiceRegistry registry, Object sender = null)
        {
            if (disposed || executed)
                return;

            executed = true;

#if UNITY_EDITOR_MODE
            debugConfig = null;
            registry.TryGet(out debugConfig);

            if (debugConfig != null && debugConfig.Enabled)
            {
                if (debugConfig.ValidateUnityCallbacks)
                    ValidateUnityCallbacks();

                if (debugConfig.LogLocalConnectorExecute)
                {
                    var senderName = sender != null ? sender.GetType().Name : "Null";
                    Debug.Log($"LocalConnector Execute: {name} -> {senderName}", this);
                }
            }
#endif

            PruneMissingNodes(rebuildCaches: false);
            SortNodes();
            RebuildCaches();

            CallBind(registry);
            CallConstruct(registry);
            CallBeforeInit();
            CallInit();
            CallAfterInit();
        }

        public void Tick(float deltaTime)
        {
            if (!enabledTicks || disposed)
                return;

            StepLogger(nameof(Tick));

            for (var i = 0; i < tickCache.Count; i++)
            {
                if (ShouldRun(tickMonos[i]))
                    tickCache[i].Tick(deltaTime);
            }
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!enabledTicks || disposed)
                return;

            StepLogger(nameof(FixedTick));

            for (var i = 0; i < fixedTickCache.Count; i++)
            {
                if (ShouldRun(fixedTickMonos[i]))
                    fixedTickCache[i].FixedTick(fixedDeltaTime);
            }
        }

        public void LateTick(float deltaTime)
        {
            if (!enabledTicks || disposed)
                return;

            StepLogger(nameof(LateTick));

            for (var i = 0; i < lateTickCache.Count; i++)
            {
                if (ShouldRun(lateTickMonos[i]))
                    lateTickCache[i].LateTick(deltaTime);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            if (!Application.isPlaying)
                return;

            disposed = true;

            TryUnregisterFromScene(force: true);

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IDispose disposable)
                    disposable.Dispose();
            }

            tickCache.Clear();
            tickMonos.Clear();
            fixedTickCache.Clear();
            fixedTickMonos.Clear();
            lateTickCache.Clear();
            lateTickMonos.Clear();
        }

        public void OnPauseRequest(Object sender = null)
        {
            if (!enabledTicks)
                return;

            InternalSetEnabledTicks(false, sender);

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IPausable pausable)
                    pausable.OnPauseRequest(sender);
            }
        }

        public void OnResumeRequest(Object sender = null)
        {
            if (enabledTicks)
                return;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IPausable pausable)
                    pausable.OnResumeRequest(sender);
            }

            InternalSetEnabledTicks(true, sender);
        }

        [Button("Collect Nodes - Собрать все нода. Жмякни если компоненты перестали выполнять Tick();")]
        public void CollectNodes()
        {
            nodes.Clear();
            collected.Clear();

            GetComponentsInChildren(includeInactive: true, collected);

            for (var i = 0; i < collected.Count; i++)
            {
                var item = collected[i];
                if (item == null)
                    continue;

                if (item is ILocalConnectorNode)
                    nodes.Add(item);
            }

            SortNodes();
            RebuildCaches();
        }

#if UNITY_EDITOR_MODE
        public virtual void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (autoCollectOnValidate)
            {
                CollectNodes();
                return;
            }

            if (autoPruneMissingOnValidate)
                PruneMissingNodes(rebuildCaches: false);
        }
#endif

        private void SortNodes() =>
            nodes.Sort(CompareNodes);

        private int CompareNodes(MonoBehaviour a, MonoBehaviour b)
        {
            if (a == null && b == null)
                return 0;

            if (a == null)
                return 1;

            if (b == null)
                return -1;

            var orderA = a is IOrder oa ? oa.Order : 0;
            var orderB = b is IOrder ob ? ob.Order : 0;

            if (orderA != orderB)
                return orderA.CompareTo(orderB);

            return string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
        }

        private void CallBind(ServiceRegistry registry)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IBind bind)
                    bind.Bind(registry);
            }
        }

        private void CallConstruct(ServiceRegistry registry)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IConstruct construct)
                    construct.Construct(registry);
            }
        }

        private void CallBeforeInit()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IBeforeInit step)
                    step.BeforeInit();
            }
        }

        private void CallInit()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

#if UNITY_EDITOR_MODE
                if (debugConfig != null && debugConfig.Enabled && debugConfig.LogPhaseCalls && node is IInit)
                    Debug.Log($"Init: {name} -> {node.GetType().Name}", node);
#endif

                if (node is IInit step)
                    step.Init();
            }
        }

        private void CallAfterInit()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IAfterInit step)
                    step.AfterInit();
            }
        }

        private bool ShouldRun(MonoBehaviour node)
        {
            if (node == null)
                return false;

            if (node.isActiveAndEnabled)
                return true;

            if (node is ConnectorNode connectorNode)
                return connectorNode.RunWhenDisabled;

            return false;
        }

        private void TryUnregisterFromScene(bool force)
        {
            if (!Application.isPlaying)
                return;

            if (!force && !dynamicRegistered)
                return;

            if (!SceneConnectorRegistry.TryGet(gameObject.scene, out var sceneConnector) || sceneConnector == null)
            {
                dynamicRegistered = false;
                return;
            }

            if (!sceneConnector.IsInitialized)
                return;

            sceneConnector.Unregister(this);
            dynamicRegistered = false;
        }

        private int PruneMissingNodes(bool rebuildCaches)
        {
            var removed = 0;

            for (var i = nodes.Count - 1; i >= 0; i--)
            {
                if (nodes[i] != null)
                    continue;

                nodes.RemoveAt(i);
                removed++;
            }

            if (removed > 0 && rebuildCaches)
                RebuildCaches();

            return removed;
        }

        private void RebuildCaches()
        {
            tickCache.Clear();
            tickMonos.Clear();

            fixedTickCache.Clear();
            fixedTickMonos.Clear();

            lateTickCache.Clear();
            lateTickMonos.Clear();

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is ITick t)
                {
                    tickCache.Add(t);
                    tickMonos.Add(node);
                }

                if (node is IFixedTick ft)
                {
                    fixedTickCache.Add(ft);
                    fixedTickMonos.Add(node);
                }

                if (node is ILateTick lt)
                {
                    lateTickCache.Add(lt);
                    lateTickMonos.Add(node);
                }
            }
        }

        private void InternalSetEnabledTicks(bool value, Object sender = null)
        {
            enabledTicks = value;

#if UNITY_EDITOR_MODE
            if (sender != null)
            {
                Debug.Log(!enabledTicks
                    ? $"-> Pause | Sender: {sender.GetType().Name}.cs"
                    : $"-> Resume | Sender: {sender.GetType().Name}.cs");
            }
#endif
        }

        private void StepLogger(string label)
        {
#if UNITY_EDITOR_MODE
            if (debugConfig == null || !debugConfig.Enabled || !debugConfig.LogTicks)
                return;

            var filter = debugConfig.LogTicksOnlyForConnectorName;

            if (!string.IsNullOrEmpty(filter) && !string.Equals(filter, name))
                return;

            Debug.Log($"{label}: {name}", this);
#endif
        }

#if UNITY_EDITOR_MODE
        private void ValidateUnityCallbacks()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                var type = node.GetType();

                if (HasUnityCallback(type, methodName: "Awake") ||
                    HasUnityCallback(type, methodName: "Start") ||
                    HasUnityCallback(type, methodName: "Update") ||
                    HasUnityCallback(type, methodName: "FixedUpdate") ||
                    HasUnityCallback(type, methodName: "LateUpdate"))
                {
                    Debug.LogError($"Unity callbacks are not allowed in ConnectorNode: {type.Name}", node);
                }
            }
        }

        private static bool HasUnityCallback(System.Type type, string methodName)
        {
            var current = type;

            while (current != null && current != typeof(MonoBehaviour))
            {
                const BindingFlags flags = BindingFlags.Instance |
                                           BindingFlags.Public |
                                           BindingFlags.NonPublic |
                                           BindingFlags.DeclaredOnly;

                var method = current.GetMethod(methodName, flags);
                if (method != null)
                    return true;

                current = current.BaseType;
            }

            return false;
        }
#endif
    }
}