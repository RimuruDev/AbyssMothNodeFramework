#if UNITY_EDITOR
using UnityEditor;
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
    public sealed class LocalConnector : MonoBehaviour, IOrder
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
        
#if UNITY_EDITOR
        private ConnectorDebugConfig debugConfig;

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

            var removed = PruneMissingNodes();
            if (removed <= 0)
                return;

            Undo.RecordObject(this, "Prune Missing Nodes");
            EditorUtility.SetDirty(this);
            EditorSceneManager.MarkAllScenesDirty();
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

            TryUnregisterFromScene(force: false);
        }

        public void OnDestroy()
        {
            if (!Application.isPlaying)
                return;

            if (disposed)
                return;

            TryUnregisterFromScene(force: true);
        }

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

            PruneMissingNodes();
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
            if (disposed)
                return;
            
            if (!enabledTicks)
                return;

            StepLogger(nameof(Tick));
            
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                
                if (node == null)
                    continue;

                if (!ShouldRun(node))
                    continue;

                if (node is ITick tick)
                    tick.Tick(deltaTime);
            }
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (disposed)
                return;

            if (!enabledTicks)
                return;
            
            StepLogger(nameof(FixedTick));

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                
                if (node == null)
                    continue;

                if (!ShouldRun(node))
                    continue;

                if (node is IFixedTick tick)
                    tick.FixedTick(fixedDeltaTime);
            }
        }

        public void LateTick(float deltaTime)
        {
            if (disposed)
                return;
            
            if (!enabledTicks)
                return;
            
            StepLogger(nameof(LateTick));

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                
                if (node == null)
                    continue;

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

            TryUnregisterFromScene(force: true);

            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                
                if (node == null)
                    continue;

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
            GetComponentsInChildren(true, collected);

            for (var i = 0; i < collected.Count; i++)
            {
                var item = collected[i];

                if (item == null)
                    continue;
                
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

            if (autoCollectOnValidate)
            {
                CollectNodes();
                return;
            }

            if (autoPruneMissingOnValidate)
                PruneMissingNodes();
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
        
        private int PruneMissingNodes()
        {
            var removed = 0;

            for (var i = nodes.Count - 1; i >= 0; i--)
            {
                if (nodes[i] != null)
                    continue;

                nodes.RemoveAt(i);
                removed++;
            }

            return removed;
        }

        private void InternalSetEnabledTicks(bool value, Object sender = null)
        {
            enabledTicks = value;

#if UNITY_EDITOR
            if (sender)
            {
                Debug.Log(!enabledTicks
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

        private void StepLogger(string label)
        {
#if UNITY_EDITOR
            if (debugConfig != null && debugConfig.Enabled && debugConfig.LogTicks)
            {
                var filter = debugConfig.LogTicksOnlyForConnectorName;

                if (!string.IsNullOrEmpty(filter) && !string.Equals(filter, name)) { }
                else
                    Debug.Log($"{label}: {name}", context: this);
            }
#endif
        }

#endif

    }
}