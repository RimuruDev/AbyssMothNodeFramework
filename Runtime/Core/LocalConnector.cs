using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    [DisallowMultipleComponent]
    public sealed class LocalConnector : MonoBehaviour, ILocalConnectorOrder
    {
        [BoxGroup("Order")]
        [SerializeField, Min(0)] private int order;
        public int Order => order;

        [BoxGroup("State")]
        [SerializeField] private bool enabledTicks = true;

        [BoxGroup("Nodes")]
        [SerializeField, ReorderableList] private List<MonoBehaviour> nodes = new();

        private readonly List<MonoBehaviour> collected = new(capacity: 64);

        private bool executed;
        private bool dynamicRegistered;
        private int staticSceneHandle = -1;

        public bool EnabledTicks => enabledTicks;

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
            if (executed)
                return;

            executed = true;

            SortNodes();

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

                if (node is ILateTick tick)
                    tick.LateTick(deltaTime);
            }
        }

        public void Dispose()
        {
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];

                if (node is IDispose disposable)
                    disposable.Dispose();
            }
        }

        public void SetEnabledTicks(bool value) => 
            enabledTicks = value;

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

        public void OnValidate()
        {
            if (!Application.isPlaying && nodes.Count == 0)
                CollectNodes();
        }

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
                if (nodes[i] is IInit step)
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
    }
}