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
        [BoxGroup("Entity")]
        [SerializeField, ReadOnly] private int entityId;

        [BoxGroup("Entity")]
        [SerializeField] private string entityTag;

        [BoxGroup("Order")]
        [SerializeField] private int order;

        public virtual int Order
        {
            get => order;
            protected set => order = value;
        }

        [BoxGroup("State")]
        [SerializeField] private bool enabledTicks = true;

        [BoxGroup("Nodes")]
        [SerializeField, ReorderableList] private List<MonoBehaviour> nodes = new();

        private readonly List<MonoBehaviour> collected = new(capacity: 64);

        private bool executed;
        private bool dynamicRegistered;
        private int staticSceneHandle = -1;
        private bool disposed;

        public int EntityId => entityId;
        public string EntityTag => entityTag;
        public bool HasEntityTag => !string.IsNullOrWhiteSpace(entityTag);
        public bool EnabledTicks => enabledTicks;

        public IReadOnlyList<MonoBehaviour> Nodes => nodes;

        private readonly List<ITick> tickCache = new();
        private readonly List<MonoBehaviour> tickMonos = new();

        private readonly List<IFixedTick> fixedTickCache = new();
        private readonly List<MonoBehaviour> fixedTickMonos = new();

        private readonly List<ILateTick> lateTickCache = new();
        private readonly List<MonoBehaviour> lateTickMonos = new();
        
        private readonly HashSet<Object> pauseOwners = new(ReferenceComparer<Object>.Instance);
        private bool pausedTicks;

        public bool IsPaused => pausedTicks;
        public bool EffectiveTicks => enabledTicks && !pausedTicks;

        public void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!SceneConnectorRegistry.TryGet(gameObject.scene, out var sceneConnector))
                return;

            if (!sceneConnector.CanRegisterRuntimeConnectors)
                return;

            var sceneHandle = gameObject.scene.handle;

            if (IsStaticFor(sceneHandle))
            {
                Execute(sceneConnector.SceneContext, sender: sceneConnector);
                return;
            }

            if (dynamicRegistered)
            {
                Execute(sceneConnector.SceneContext, sender: sceneConnector);
                return;
            }

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

        internal void SetEntityIdInternal(int value)
        {
            entityId = value < 0
                ? 0
                : value;
        }

        internal void SetDynamicRegisteredInternal(bool value) =>
            dynamicRegistered = value;

        internal bool IsStaticFor(int sceneHandle) =>
            staticSceneHandle == sceneHandle && staticSceneHandle != -1;

        public void SetEntityTag(string value)
        {
            var normalized = NormalizeTag(value);

            if (string.Equals(entityTag, normalized, System.StringComparison.Ordinal))
                return;

            entityTag = normalized;

            if (!Application.isPlaying)
                return;

            if (!SceneConnectorRegistry.TryGet(gameObject.scene, out var sceneConnector) || sceneConnector == null)
                return;

            if (!sceneConnector.CanRegisterRuntimeConnectors)
                return;

            if (!sceneConnector.SceneContext.TryGet(out SceneEntityIndex sceneIndex) || sceneIndex == null)
                return;

            sceneIndex.Refresh(this);
        }

        public void Execute(ServiceContainer registry, Object sender = null)
        {
            if (disposed || executed)
                return;

            executed = true;

#if UNITY_EDITOR_MODE
            if (FrameworkLogger.ShouldValidateNodeCallbacks())
                ValidateUnityCallbacks();
#endif
            if (FrameworkLogger.ShouldLogConnectorExecute())
            {
                var senderName = sender != null ? sender.GetType().Name : "Null";
                FrameworkLogger.Info($"LocalConnector Execute: {name} -> {senderName}", this);
            }

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
            if (!enabledTicks || pausedTicks || disposed)
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
            if (!enabledTicks || pausedTicks || disposed)
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
            if (!enabledTicks || pausedTicks || disposed)
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
            
            pauseOwners.Clear();
            pausedTicks = false;

            tickCache.Clear();
            tickMonos.Clear();
            fixedTickCache.Clear();
            fixedTickMonos.Clear();
            lateTickCache.Clear();
            lateTickMonos.Clear();
        }

        public void OnPauseRequest(Object sender = null)
        {
            if (disposed)
                return;

            var owner = sender != null ? sender : this;

            if (!pauseOwners.Add(owner))
                return;

            if (pauseOwners.Count != 1)
                return;

            InternalSetPausedTicks(true, sender);

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IPausable pausable)
                    pausable.OnPauseRequest(owner);
            }
        }

        public void OnResumeRequest(Object sender = null)
        {
            if (disposed)
                return;

            var owner = sender != null ? sender : this;

            if (!pauseOwners.Remove(owner))
                return;

            if (pauseOwners.Count > 0)
                return;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IPausable pausable)
                    pausable.OnResumeRequest(owner);
            }

            InternalSetPausedTicks(false, sender);
        }

        [Button("Rebuild Node Cache")]
        public void CollectNodes() =>
            CollectNodesCore(markDirtyIfChanged: !Application.isPlaying, rebuildCachesEvenIfSame: true);

        private bool CollectNodesCore(bool markDirtyIfChanged, bool rebuildCachesEvenIfSame)
        {
            collected.Clear();
            GetComponentsInChildren(includeInactive: true, collected);

            var next = new List<MonoBehaviour>(capacity: collected.Count);

            for (var i = 0; i < collected.Count; i++)
            {
                var item = collected[i];

                if (item == null)
                    continue;

                if (item is ILocalConnectorNode)
                    next.Add(item);
            }

            next.Sort(CompareNodes);

#if UNITY_EDITOR_MODE
            if (!Application.isPlaying)
            {
                if (IsSameNodes(nodes, next))
                    return false;

                nodes.Clear();
                nodes.AddRange(next);

                if (markDirtyIfChanged)
                {
                    EditorUtility.SetDirty(this);

                    if (gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(gameObject.scene);
                }

                RebuildCaches();
                return true;
            }
#endif

            if (IsSameNodes(nodes, next))
            {
                if (rebuildCachesEvenIfSame)
                    RebuildCaches();

                return false;
            }

            nodes.Clear();
            nodes.AddRange(next);
            RebuildCaches();
            return true;
        }

        private static bool IsSameNodes(List<MonoBehaviour> a, List<MonoBehaviour> b)
        {
            if (a == null || b == null)
                return a == b;

            if (a.Count != b.Count)
                return false;

            for (var i = 0; i < a.Count; i++)
            {
                if (!ReferenceEquals(a[i], b[i]))
                    return false;
            }

            return true;
        }

#if UNITY_EDITOR_MODE
        public virtual void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (entityId < 0)
                entityId = 0;

            entityTag = NormalizeTag(entityTag);
            CollectNodesCore(markDirtyIfChanged: true, rebuildCachesEvenIfSame: false);
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

        private void CallBind(ServiceContainer registry)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IBind bind)
                {
                    MarkNodeLifecycleEntered(node);
                    bind.Bind(registry);
                }
            }
        }

        private void CallConstruct(ServiceContainer registry)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                if (node == null)
                    continue;

                if (node is IConstruct construct)
                {
                    MarkNodeLifecycleEntered(node);
                    construct.Construct(registry);
                }
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
                {
                    MarkNodeLifecycleEntered(node);
                    step.BeforeInit();
                }
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
                if (FrameworkLogger.ShouldLogNodePhases() && node is IInit)
                    FrameworkLogger.Verbose($"Init: {name} -> {node.GetType().Name}", node);
#endif

                if (node is IInit step)
                {
                    MarkNodeLifecycleEntered(node);
                    step.Init();
                }
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
                {
                    MarkNodeLifecycleEntered(node);
                    step.AfterInit();
                }
            }
        }

        private static void MarkNodeLifecycleEntered(MonoBehaviour node)
        {
            if (node is ConnectorNode connectorNode)
                connectorNode.MarkLifecycleEntered();
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

            if (!sceneConnector.CanRegisterRuntimeConnectors)
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

        private void InternalSetPausedTicks(bool value, Object sender = null)
        {
            pausedTicks = value;

#if UNITY_EDITOR_MODE
            if (sender != null)
            {
                FrameworkLogger.Verbose(!pausedTicks
                    ? $"-> Resume | Sender: {sender.GetType().Name}.cs"
                    : $"-> Pause | Sender: {sender.GetType().Name}.cs", this);
            }
#endif
        }

        private void StepLogger(string label)
        {
            if (!FrameworkLogger.ShouldLogTick(name))
                return;

            FrameworkLogger.Verbose($"{label}: {name}", this);
        }

        private static string NormalizeTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value.Trim();
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
                    FrameworkLogger.Error($"Unity callbacks are not allowed in ConnectorNode: {type.Name}", node);
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
