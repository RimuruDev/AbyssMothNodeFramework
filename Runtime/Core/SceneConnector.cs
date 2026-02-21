#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

using System;
using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Scripting;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AbyssMoth
{
    [Preserve]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-900)]
    [AddComponentMenu("AbyssMoth Node Framework/Scene Connector")]
    public sealed class SceneConnector : MonoBehaviour
    {
        public event Action<SceneConnector> SceneInitialized;
        
        [BoxGroup("Connectors")]
        [SerializeField, ReorderableList] private List<LocalConnector> connectors = new();

        [BoxGroup("Dynamic")]
        [SerializeField] private bool autoRegisterActiveUnbakedConnectors = true;
        private SceneEntityIndex sceneIndex;
        private ServiceContainer sceneContext;

        private readonly List<LocalConnector> dynamicConnectors = new(capacity: 64);
        private readonly HashSet<LocalConnector> dynamicSet = new(ReferenceComparer<LocalConnector>.Instance);

        private readonly List<LocalConnector> pendingAdd = new(capacity: 32);
        private readonly List<LocalConnector> pendingRemove = new(capacity: 32);

        private readonly List<LocalConnector> scanBuffer = new(capacity: 128);
        private readonly List<LocalConnector> collectBuffer = new(capacity: 128);

        private bool executed;
        private bool initialized;
        private bool iterating;
        private int sceneHandle;
        
        public bool IsInitialized => initialized;
        public ServiceContainer SceneContext => sceneContext;
        internal bool CanRegisterRuntimeConnectors =>
            sceneContext != null && sceneIndex != null;
        
        private readonly HashSet<Object> pauseOwners = new(ReferenceComparer<Object>.Instance);
        private readonly List<Object> pauseOwnersBuffer = new(capacity: 8);

        public bool IsPaused => pauseOwners.Count > 0;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            var scene = gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                return;

            CollectConnectorsCore(markDirtyIfChanged: true);
        }

        [Button("Log Scene Index Dump")]
        private void LogSceneIndexDump()
        {
            if (sceneIndex == null)
            {
                FrameworkLogger.Warning("SceneEntityIndex is null", this);
                return;
            }

            var dump = sceneIndex.BuildDump();
            FrameworkLogger.Info(dump, this);
        }
#endif

        public void Awake() => 
            SceneConnectorRegistry.TryRegister(connector: this);

        public void OnDestroy()
        {
            SceneConnectorRegistry.Unregister(connector: this);

            if (!CanRegisterRuntimeConnectors)
                return;

            for (var i = 0; i < connectors.Count; i++)
            {
                var connector = connectors[i];
                if (connector != null)
                    connector.Dispose();
            }

            for (var i = 0; i < dynamicConnectors.Count; i++)
            {
                var connector = dynamicConnectors[i];
                if (connector != null)
                    connector.Dispose();
            }

            dynamicConnectors.Clear();
            dynamicSet.Clear();
            sceneContext?.Clear();
            sceneIndex?.Clear();
        }

        public void Execute(ServiceContainer projectContext)
        {
            if (executed)
                return;

            executed = true;
            using var traceScope = new FrameworkInitializationTrace.Scope(
                $"SceneConnector.Execute: {name} (scene={gameObject.scene.name})",
                this);

            sceneHandle = gameObject.scene.handle;
            sceneContext = new ServiceContainer(parentContainer: projectContext);
            sceneContext.Add(this);
            
            sceneIndex = new SceneEntityIndex();
            sceneContext.Add(sceneIndex);

            var config = FrameworkConfig.TryLoadDefault();
            if (config != null)
            {
                FrameworkLogger.Configure(config);

                if (!sceneContext.TryGet(out FrameworkConfig _))
                    sceneContext.Add(config);
            }

            PruneMissingConnectors();
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

            FrameworkLogger.Boot($"SceneConnector.Execute({name})", this);

            sceneIndex.PrimeReservedIds(connectors, sceneHandle);
            
            PreRegisterStaticConnectors();
            ExecuteStaticConnectors();

            if (autoRegisterActiveUnbakedConnectors)
                RegisterActiveUnbakedConnectors();

            initialized = true;
            SceneInitialized?.Invoke(this);
        }

        public void RegisterAndExecute(LocalConnector connector)
        {
            if (!CanRegisterRuntimeConnectors || connector == null)
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

        public void RegisterSpawned(LocalConnector connector) =>
            RegisterAndExecute(connector);

        public void RegisterSpawnedHierarchy(GameObject root)
        {
            if (!CanRegisterRuntimeConnectors || root == null)
                return;

            scanBuffer.Clear();
            root.GetComponentsInChildren(includeInactive: true, results: scanBuffer);
            scanBuffer.Sort(CompareConnectors);

            for (var i = 0; i < scanBuffer.Count; i++)
            {
                var connector = scanBuffer[i];
                if (connector == null)
                    continue;

                if (connector.IsStaticFor(sceneHandle))
                    continue;

                RegisterAndExecute(connector);
            }

            scanBuffer.Clear();
        }

        public T InstantiateAndRegister<T>(T prefab, Transform parent = null, bool worldPositionStays = false)
            where T : LocalConnector
        {
            if (prefab == null)
                return null;

            var instance = parent == null
                ? Instantiate(prefab)
                : Instantiate(prefab, parent, worldPositionStays);

            RegisterSpawnedHierarchy(instance.gameObject);
            return instance;
        }

        public T InstantiateAndRegister<T>(
            T prefab,
            Vector3 position,
            Quaternion rotation,
            Transform parent = null) where T : LocalConnector
        {
            if (prefab == null)
                return null;

            var instance = parent == null
                ? Instantiate(prefab, position, rotation)
                : Instantiate(prefab, position, rotation, parent);

            RegisterSpawnedHierarchy(instance.gameObject);
            return instance;
        }

        public void Unregister(LocalConnector connector)
        {
            if (!CanRegisterRuntimeConnectors || connector == null)
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
        public void CollectConnectors() =>
            CollectConnectorsCore(markDirtyIfChanged: !Application.isPlaying);
        
        public void OnPauseRequest(Object sender = null)
        {
            if (!initialized)
                return;

            var owner = sender != null ? sender : this;

            if (!pauseOwners.Add(owner))
                return;

            for (var i = 0; i < connectors.Count; i++)
            {
                var c = connectors[i];
                if (c != null)
                    c.OnPauseRequest(owner);
            }

            for (var i = 0; i < dynamicConnectors.Count; i++)
            {
                var c = dynamicConnectors[i];
                if (c != null)
                    c.OnPauseRequest(owner);
            }
        }

        public void OnResumeRequest(Object sender = null)
        {
            if (!initialized)
                return;

            var owner = sender != null ? sender : this;

            if (!pauseOwners.Remove(owner))
                return;

            for (var i = 0; i < connectors.Count; i++)
            {
                var c = connectors[i];
                if (c != null)
                    c.OnResumeRequest(owner);
            }

            for (var i = 0; i < dynamicConnectors.Count; i++)
            {
                var c = dynamicConnectors[i];
                if (c != null)
                    c.OnResumeRequest(owner);
            }
        }
        
        private void ApplyPauseOwners(LocalConnector connector)
        {
            if (connector == null)
                return;

            if (pauseOwners.Count == 0)
                return;

            pauseOwnersBuffer.Clear();

            foreach (var owner in pauseOwners)
                pauseOwnersBuffer.Add(owner);

            for (var i = 0; i < pauseOwnersBuffer.Count; i++)
                connector.OnPauseRequest(pauseOwnersBuffer[i]);
        }

        private bool CollectConnectorsCore(bool markDirtyIfChanged)
        {
            var next = new List<LocalConnector>(capacity: 64);
            CollectSceneConnectors(scene: gameObject.scene, includeInactive: true, output: next);

            next.Sort(CompareConnectors);

            if (IsSameConnectors(connectors, next))
                return false;

            connectors.Clear();
            connectors.AddRange(next);

#if UNITY_EDITOR
            if (!Application.isPlaying && markDirtyIfChanged)
            {
                EditorUtility.SetDirty(this);

                if (gameObject.scene.IsValid())
                    EditorSceneManager.MarkSceneDirty(gameObject.scene);
            }
#endif
            return true;
        }

        private static bool IsSameConnectors(List<LocalConnector> a, List<LocalConnector> b)
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

        private void RegisterActiveUnbakedConnectors()
        {
            CollectSceneConnectors(scene: gameObject.scene, includeInactive: false, output: scanBuffer);
            scanBuffer.Sort(CompareConnectors);

            for (var i = 0; i < scanBuffer.Count; i++)
            {
                var connector = scanBuffer[i];

                if (connector == null)
                    continue;

                if (connector.gameObject.scene.handle != sceneHandle)
                    continue;

                if (!connector.isActiveAndEnabled)
                    continue;

                if (connector.IsStaticFor(sceneHandle))
                    continue;

                RegisterInternal(connector);
            }

            scanBuffer.Clear();
        }

        private void CollectSceneConnectors(Scene scene, bool includeInactive, List<LocalConnector> output)
        {
            output.Clear();

            if (!scene.IsValid() || !scene.isLoaded)
                return;

            var roots = scene.GetRootGameObjects();

            for (var i = 0; i < roots.Length; i++)
            {
                var root = roots[i];

                if (root == null)
                    continue;

                collectBuffer.Clear();
                root.GetComponentsInChildren(includeInactive, collectBuffer);

                for (var j = 0; j < collectBuffer.Count; j++)
                {
                    var connector = collectBuffer[j];

                    if (connector == null)
                        continue;

                    if (connector.gameObject.scene != scene)
                        continue;

                    if (connector.GetComponentInParent<ProjectRootConnector>(includeInactive: true) != null)
                        continue;

                    output.Add(connector);
                }
            }

            collectBuffer.Clear();
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

            using var traceScope = new FrameworkInitializationTrace.Scope(
                $"Runtime Register: {connector.name}",
                connector);

            sceneIndex.Register(connector);

            if (connector.isActiveAndEnabled)
                connector.Execute(sceneContext, sender: this);

            ApplyPauseOwners(connector);

            var index = GetInsertIndex(connector);
            dynamicConnectors.Insert(index, connector);
            dynamicSet.Add(connector);
            connector.SetDynamicRegisteredInternal(true);
        }

        private void UnregisterInternal(LocalConnector connector)
        {
            if (connector == null)
                return;
            
            sceneIndex.Unregister(connector);
            connector.SetDynamicRegisteredInternal(false);

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
                {
                    using var traceScope = new FrameworkInitializationTrace.Scope(
                        $"Static Connector: {connector.name}",
                        connector);
                    connector.Execute(sceneContext, sender: this);
                }
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

            var oa = a is IOrder la ? la.Order : 0;
            var ob = b is IOrder lb ? lb.Order : 0;

            if (oa != ob)
                return oa.CompareTo(ob);

            return string.CompareOrdinal(a.name, b.name);
        }
        
        private int PruneMissingConnectors()
        {
            var removed = 0;

            for (var i = connectors.Count - 1; i >= 0; i--)
            {
                if (connectors[i] != null)
                    continue;

                connectors.RemoveAt(i);
                removed++;
            }

            return removed;
        }
    }
}
