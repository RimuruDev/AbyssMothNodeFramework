using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Scripting;
using System.Collections.Generic;

namespace AbyssMoth
{
    [Preserve]
    [DisallowMultipleComponent]
    public sealed class LocalConnector : MonoBehaviour, ILocalConnectorOrder
    {
        [BoxGroup("Order")]
        [SerializeField, Min(-1)] private int order;
        public int Order => order;

        [BoxGroup("State")]
        [SerializeField] private bool enabledTicks = true;

        [BoxGroup("Nodes")]
        [SerializeField, ReorderableList] private List<MonoBehaviour> nodes = new();

#if UNITY_EDITOR
        [BoxGroup("Debug")]
        [SerializeField] private bool autoCollectOnValidate;
#endif
        private readonly List<MonoBehaviour> collected = new(capacity: 64);

        private bool executed;
        private bool dynamicRegistered;
        private int staticSceneHandle = -1;
        private bool disposed;

        public bool EnabledTicks => enabledTicks;

#if UNITY_EDITOR
        private ConnectorDebugConfig debugConfig;
#endif
        
#if UNITY_EDITOR
        [ShowNativeProperty] private int NodesCount => nodes.Count;
        [ShowNativeProperty] private bool Executed => executed;
        [ShowNativeProperty] private bool DynamicRegistered => dynamicRegistered;
        [ShowNativeProperty] private bool IsStaticForCurrentScene => IsStaticFor(gameObject.scene.handle);

        [ShowNativeProperty] private string NodeOrder
        {
            get
            {
                var sb = new System.Text.StringBuilder();

                for (var i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    var nodeOrder = n is IConnectorOrder o ? o.Order : 0;
                    sb.AppendLine($"{nodeOrder} | {n.GetType().Name}");
                }

                return sb.ToString();
            }
        }
#endif

        public void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (dynamicRegistered)
                return;

            if (IsStaticFor(gameObject.scene.handle))
                return;

            if (!SceneConnectorRegistry.TryGet(gameObject.scene, out var sceneConnector))
                return;

            if (!sceneConnector.IsInitialized)
                return;

            sceneConnector.RegisterAndExecute(this);
            dynamicRegistered = true;
        }

        public void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (!dynamicRegistered)
                return;

            if (!SceneConnectorRegistry.TryGet(gameObject.scene, out var sceneConnector) || sceneConnector == null)
            {
                dynamicRegistered = false;
                return;
            }

            sceneConnector.Unregister(this);
            dynamicRegistered = false;
        }

        public void OnDestroy() => 
            OnDisable();

        internal void MarkStatic(int sceneHandle) => 
            staticSceneHandle = sceneHandle;

        internal bool IsStaticFor(int sceneHandle) => 
            staticSceneHandle == sceneHandle && staticSceneHandle != -1;

        public void Execute(ServiceRegistry registry)
        {
            if (disposed)
                return;

            if (executed)
                return;

            executed = true;

            SortNodes();

#if UNITY_EDITOR
            debugConfig = null;
            registry.TryGet(out debugConfig);

            if (debugConfig != null && debugConfig.Enabled)
            {
                if (debugConfig.ValidateUnityCallbacks)
                    ValidateUnityCallbacks();

                if (debugConfig.LogLocalConnectorExecute)
                    Debug.Log($"LocalConnector Execute: {name}", this);
            }
#endif

            CallBind(registry);
            CallConstruct(registry);
            CallBeforeInit();
            CallInit();
            CallAfterInit();
        }

        public void Tick(float deltaTime)
        {
            if (!enabledTicks)
                return;

#if UNITY_EDITOR
            if (debugConfig != null && debugConfig.Enabled && debugConfig.LogTicks)
            {
                var filter = debugConfig.LogTicksOnlyForConnectorName;

                if (!string.IsNullOrEmpty(filter) && !string.Equals(filter, name)) { }
                else
                {
                    Debug.Log($"Tick: {name}", this);
                }
            }
#endif

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

                if (!ShouldRun(node))
                    continue;

                if (node is ITick tick)
                    tick.Tick(deltaTime);
            }
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!enabledTicks)
                return;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                
                if (!ShouldRun(node))
                    continue;

                if (node is IFixedTick tick)
                    tick.FixedTick(fixedDeltaTime);
            }
        }

        public void LateTick(float deltaTime)
        {
            if (!enabledTicks)
                return;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                
                if (!ShouldRun(node))
                    continue;

                if (node is ILateTick tick)
                    tick.LateTick(deltaTime);
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

                if (node is IDispose disposable)
                    disposable.Dispose();
            }
        }

        public void OnPauseRequest(Object sender = null)
        {
            if (!enabledTicks)
                return;
            
            InternalSetEnabledTicks(false, sender);

            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is IPausable pausable)
                    pausable.OnPauseRequest(sender);
            }
        }
        
        public void OnResumeRequest(Object sender = null)
        {
            if (enabledTicks)
                return;
            
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is IPausable pausable)
                    pausable.OnResumeRequest(sender);
            }
            
            InternalSetEnabledTicks(true, sender);
        }

        [Button("Collect Nodes - Собрать все нода. Жмякни если компоненты перестали выполнять Tick();")]
        public void CollectNodes()
        {
            nodes.Clear();

            collected.Clear();
            GetComponentsInChildren(true, collected);

            for (var i = 0; i < collected.Count; i++)
            {
                var item = collected[i];

                if (item is ILocalConnectorNode)
                    nodes.Add(item);
            }

            SortNodes();
        }

#if UNITY_EDITOR
        public void OnValidate()
        {
            if (Application.isPlaying)
                return;
            
            if (!autoCollectOnValidate)
                return;

            CollectNodes();
        }
#endif

        private void SortNodes() => 
            nodes.Sort(CompareNodes);

        private int CompareNodes(MonoBehaviour a, MonoBehaviour b)
        {
            var orderA = a is IConnectorOrder oa ? oa.Order : 0;
            var orderB = b is IConnectorOrder ob ? ob.Order : 0;

            if (orderA != orderB)
                return orderA.CompareTo(orderB);

            return string.CompareOrdinal(a.GetType().Name, b.GetType().Name);
        }

        private void CallBind(ServiceRegistry registry)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is IBind bind)
                    bind.Bind(registry);
            }
        }

        private void CallConstruct(ServiceRegistry registry)
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is IConstruct construct)
                    construct.Construct(registry);
            }
        }

        private void CallBeforeInit()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is IBeforeInit step)
                    step.BeforeInit();
            }
        }

        private void CallInit()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

#if UNITY_EDITOR
                if (debugConfig != null && debugConfig.Enabled && debugConfig.LogPhaseCalls)
                {
                    if (node != null && node is IInit)
                        Debug.Log($"Init: {name} -> {node.GetType().Name}", node);
                }
#endif

                if (node is IInit step)
                    step.Init();
            }
        }

        private void CallAfterInit()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                if (nodes[i] is IAfterInit step)
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
        
        private void InternalSetEnabledTicks(bool value, Object sender = null)
        {
            enabledTicks = value;

#if UNITY_EDITOR
            if (sender)
            {
                Debug.Log(enabledTicks
                    ? $"<color=yellow>-> Pause | Sender: <color=green>{sender.GetType().Name}.cs</color></color>"
                    : $"<color=yellow>-> Resume | Sender: <color=green>{sender.GetType().Name}.cs</color></color>");
            }
#endif
        }
        
#if UNITY_EDITOR
        private void ValidateUnityCallbacks()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

                if (node == null)
                    continue;

                var type = node.GetType();

                if (HasUnityCallback(type, "Awake") ||
                    HasUnityCallback(type, "Start") ||
                    
                    /*HasUnityCallback(type, "OnEnable") ||
                    HasUnityCallback(type, "OnDisable") ||*/
                    
                    HasUnityCallback(type, "Update") ||
                    HasUnityCallback(type, "FixedUpdate") ||
                    HasUnityCallback(type, "LateUpdate"))
                {
                    Debug.LogError($"Unity callbacks are not allowed in ConnectorNode: {type.Name}", node);
                }
            }
        }
        
#if UNITY_EDITOR
        private static bool HasUnityCallback(System.Type type, string methodName)
        {
            var current = type;

            while (current != null && current != typeof(MonoBehaviour))
            {
                var flags = System.Reflection.BindingFlags.Instance |
                            System.Reflection.BindingFlags.Public |
                            System.Reflection.BindingFlags.NonPublic |
                            System.Reflection.BindingFlags.DeclaredOnly;

                var method = current.GetMethod(methodName, flags);
                if (method != null)
                    return true;

                current = current.BaseType;
            }

            return false;
        }
#endif

#endif

    }
}