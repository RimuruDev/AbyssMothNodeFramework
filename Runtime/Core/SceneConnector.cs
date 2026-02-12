using System;
using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Scripting;
using System.Collections.Generic;

namespace AbyssMoth
{
    [Preserve]
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public sealed class SceneConnector : MonoBehaviour
    {
        public event Action<SceneConnector> SceneInitialized;
        
        [BoxGroup("Connectors")]
        [SerializeField, ReorderableList] private List<LocalConnector> connectors = new();

        [BoxGroup("Dynamic")]
        [SerializeField] private bool autoRegisterActiveUnbakedConnectors = true;

        [BoxGroup("Debug")]
        [SerializeField] private bool logSteps;
#if UNITY_EDITOR
        [BoxGroup("Debug")]
        [SerializeField] private ConnectorDebugConfig debugConfig;
        [BoxGroup("Debug")]
        [SerializeField] private bool autoCollectOnValidate;
        [BoxGroup("Debug")]
        [SerializeField, ReadOnly, TextArea(6, 30)] private string sceneIndexDump;
        
        [Button("Dump SceneEntityIndex")]
        private void DumpSceneIndex()
        {
            if (sceneIndex == null)
            {
                Debug.Log("SceneEntityIndex is null", this);
                return;
            }

            var dump = sceneIndex.BuildDump();
#if UNITY_EDITOR
            sceneIndexDump = dump;
#endif
            Debug.Log(dump, this);
        }
#endif
        private SceneEntityIndex sceneIndex;
        private ServiceRegistry sceneContext;

        private readonly List<LocalConnector> dynamicConnectors = new(capacity: 64);
        private readonly HashSet<LocalConnector> dynamicSet = new(ReferenceComparer<LocalConnector>.Instance);

        private readonly List<LocalConnector> pendingAdd = new(capacity: 32);
        private readonly List<LocalConnector> pendingRemove = new(capacity: 32);

        private readonly List<LocalConnector> scanBuffer = new(capacity: 128);

        [ShowNonSerializedField] private bool executed;
        [ShowNonSerializedField] private bool initialized;
        [ShowNonSerializedField] private bool iterating;
        [ShowNonSerializedField] private int sceneHandle;
        
        public bool IsInitialized => initialized;
        public ServiceRegistry SceneContext => sceneContext;

#if UNITY_EDITOR
        [ShowNativeProperty] private int DynamicCount => dynamicConnectors.Count;
        [ShowNativeProperty] private int PendingAddCount => pendingAdd.Count;
        [ShowNativeProperty] private int PendingRemoveCount => pendingRemove.Count;
        [ShowNativeProperty] private bool Iterating => iterating;

        private void OnValidate()
        {
            if (Application.isPlaying)
                return;
            
            if (!autoCollectOnValidate)
                return;

            CollectConnectors();
        }
#endif

        public void Awake() => 
            SceneConnectorRegistry.TryRegister(this);

        public void OnDestroy()
        {
            SceneConnectorRegistry.Unregister(this);

            if (!initialized)
                return;

            for (var i = 0; i < connectors.Count; i++)
            {
                var connector = connectors[i];

                if (connector != null && connector.isActiveAndEnabled)
                    connector.Dispose();
            }

            for (var i = 0; i < dynamicConnectors.Count; i++)
            {
                var connector = dynamicConnectors[i];

                if (connector != null && connector.isActiveAndEnabled)
                    connector.Dispose();
            }

            dynamicConnectors.Clear();
            dynamicSet.Clear();
            sceneContext?.Clear();
            sceneIndex?.Clear();
        }

        public void Execute(ServiceRegistry projectContext)
        {
            if (executed)
                return;

            executed = true;

            sceneHandle = gameObject.scene.handle;
            sceneContext = new ServiceRegistry(parentContainer: projectContext);
            
            sceneIndex = new SceneEntityIndex();
            sceneContext.Add(sceneIndex);
            
#if UNITY_EDITOR
            var config = debugConfig;

            if (config == null)
                config = ConnectorDebugConfig.TryLoadDefault();

            if (config != null)
                sceneContext.Add(config);
#endif
            connectors.Sort(CompareConnectors);

            for (var i = 0; i < connectors.Count; i++)
            {
                var connector = connectors[i];

                if (connector == null)
                    continue;

                if (connector.gameObject.scene.handle != sceneHandle)
                    continue;

                connector.MarkStatic(sceneHandle);
            }

            if (logSteps)
                Debug.Log("SceneConnector Execute");

            PreRegisterStaticConnectors();
            ExecuteStaticConnectors();

            if (autoRegisterActiveUnbakedConnectors)
                RegisterActiveUnbakedConnectors();

            SceneInitialized?.Invoke(this);
            initialized = true;
        }

        public void RegisterAndExecute(LocalConnector connector)
        {
            if (!initialized || connector == null)
                return;

            if (connector.gameObject.scene.handle != sceneHandle)
                return;

            if (connector.IsStaticFor(sceneHandle))
                return;

            if (iterating)
            {
                if (!pendingAdd.Contains(connector))
                    pendingAdd.Add(connector);

                return;
            }

            RegisterInternal(connector);
        }

        public void Unregister(LocalConnector connector)
        {
            if (!initialized || connector == null)
                return;

            if (iterating)
            {
                if (!pendingRemove.Contains(connector))
                    pendingRemove.Add(connector);

                return;
            }

            UnregisterInternal(connector);
        }

        public void Update()
        {
            if (!initialized)
                return;

            ApplyPending();

            var dt = Time.deltaTime;

            iterating = true;

            for (var i = 0; i < connectors.Count; i++)
            {
                var connector = connectors[i];

                if (connector != null && connector.isActiveAndEnabled && connector.gameObject.scene.handle == sceneHandle)
                    connector.Tick(dt);
            }

            for (var i = 0; i < dynamicConnectors.Count; i++)
            {
                var connector = dynamicConnectors[i];

                if (connector != null && connector.isActiveAndEnabled && connector.gameObject.scene.handle == sceneHandle)
                    connector.Tick(dt);
            }

            iterating = false;

            ApplyPending();
        }

        public void FixedUpdate()
        {
            if (!initialized)
                return;

            ApplyPending();

            var dt = Time.fixedDeltaTime;

            iterating = true;

            for (var i = 0; i < connectors.Count; i++)
            {
                var connector = connectors[i];

                if (connector != null && connector.isActiveAndEnabled && connector.gameObject.scene.handle == sceneHandle)
                    connector.FixedTick(dt);
            }

            for (var i = 0; i < dynamicConnectors.Count; i++)
            {
                var connector = dynamicConnectors[i];

                if (connector != null && connector.isActiveAndEnabled && connector.gameObject.scene.handle == sceneHandle)
                    connector.FixedTick(dt);
            }

            iterating = false;

            ApplyPending();
        }

        public void LateUpdate()
        {
            if (!initialized)
                return;

            ApplyPending();

            var dt = Time.deltaTime;

            iterating = true;

            for (var i = 0; i < connectors.Count; i++)
            {
                var connector = connectors[i];

                if (connector != null && connector.isActiveAndEnabled && connector.gameObject.scene.handle == sceneHandle)
                    connector.LateTick(dt);
            }

            for (var i = 0; i < dynamicConnectors.Count; i++)
            {
                var connector = dynamicConnectors[i];

                if (connector != null && connector.isActiveAndEnabled && connector.gameObject.scene.handle == sceneHandle)
                    connector.LateTick(dt);
            }

            iterating = false;

            ApplyPending();
        }

        [Button("Collect LocalConnectors From Scene")]
        public void CollectConnectors()
        {
            connectors.Clear();

            var scene = gameObject.scene;
            var found = FindObjectsByType<LocalConnector>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            for (var i = 0; i < found.Length; i++)
            {
                var item = found[i];

                if (item == null)
                    continue;

                if (item.gameObject.scene != scene)
                    continue;

                if (item.GetComponentInParent<ProjectRootConnector>(includeInactive: true) != null)
                    continue;

                connectors.Add(item);
            }

            connectors.Sort(CompareConnectors);
        }

        private void RegisterActiveUnbakedConnectors()
        {
            scanBuffer.Clear();

            var found = FindObjectsByType<LocalConnector>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (var i = 0; i < found.Length; i++)
            {
                var connector = found[i];

                if (connector == null)
                    continue;

                if (connector.gameObject.scene.handle != sceneHandle)
                    continue;

                if (!connector.isActiveAndEnabled)
                    continue;

                if (connector.IsStaticFor(sceneHandle))
                    continue;

                scanBuffer.Add(connector);
            }

            scanBuffer.Sort(CompareConnectors);

            for (var i = 0; i < scanBuffer.Count; i++)
                RegisterInternal(scanBuffer[i]);

            scanBuffer.Clear();
        }

        private void ApplyPending()
        {
            if (pendingRemove.Count > 0)
            {
                for (var i = 0; i < pendingRemove.Count; i++)
                    UnregisterInternal(pendingRemove[i]);

                pendingRemove.Clear();
            }

            if (pendingAdd.Count > 0)
            {
                for (var i = 0; i < pendingAdd.Count; i++)
                    RegisterInternal(pendingAdd[i]);

                pendingAdd.Clear();
            }
        }

        private void RegisterInternal(LocalConnector connector)
        {
            if (connector == null)
                return;

            if (dynamicSet.Contains(connector))
                return;

            if (connector.gameObject.scene.handle != sceneHandle)
                return;

            sceneIndex.Register(connector);
            connector.Execute(sceneContext);

            var index = GetInsertIndex(connector);
            dynamicConnectors.Insert(index, connector);
            dynamicSet.Add(connector);
        }

        private void UnregisterInternal(LocalConnector connector)
        {
            if (connector == null)
                return;
            
            sceneIndex.Unregister(connector);

            if (!dynamicSet.Remove(connector))
                return;

            dynamicConnectors.Remove(connector);
        }

        private int GetInsertIndex(LocalConnector connector)
        {
            var low = 0;
            var high = dynamicConnectors.Count;

            while (low < high)
            {
                var mid = (low + high) / 2;
                var current = dynamicConnectors[mid];
                var compare = CompareConnectors(connector, current);

                if (compare < 0)
                    high = mid;
                else
                    low = mid + 1;
            }

            return low;
        }
        
        private void PreRegisterStaticConnectors()
        {
            for (var i = 0; i < connectors.Count; i++)
            {
                var connector = connectors[i];

                if (connector == null)
                    continue;

                if (connector.gameObject.scene.handle != sceneHandle)
                    continue;

                sceneIndex.Register(connector);
            }
        }

        private void ExecuteStaticConnectors()
        {
            for (var i = 0; i < connectors.Count; i++)
            {
                var connector = connectors[i];

                if (connector == null)
                    continue;

                if (connector.gameObject.scene.handle != sceneHandle)
                    continue;

                if (connector.isActiveAndEnabled)
                    connector.Execute(sceneContext);
            }
        }
        
        private int CompareConnectors(LocalConnector a, LocalConnector b)
        {
            if (a == null && b == null)
                return 0;

            if (a == null)
                return 1;

            if (b == null)
                return -1;

            var oa = a is ILocalConnectorOrder la ? la.Order : 0;
            var ob = b is ILocalConnectorOrder lb ? lb.Order : 0;

            if (oa != ob)
                return oa.CompareTo(ob);

            return string.CompareOrdinal(a.name, b.name);
        }
    }
}