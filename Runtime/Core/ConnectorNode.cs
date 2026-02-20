#if UNITY_EDITOR
#define UNITY_EDITOR_MODE
#endif

using UnityEngine;
using NaughtyAttributes;
using System.Reflection;
using System.Collections.Generic;

#if UNITY_EDITOR_MODE
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace AbyssMoth
{
    public abstract class ConnectorNode : MonoBehaviour, 
        ILocalConnectorNode, IOrder, 
        IBind, IConstruct, IBeforeInit, IInit, IAfterInit, 
        ITick, IFixedTick, ILateTick, IPausable, IDispose
    {
        [BoxGroup("Order")]
        [SerializeField] private int order;
       
        public virtual int Order 
        { 
            get => order; 
            protected set => order = value; 
        }

        [BoxGroup("State")]
        [SerializeField] private bool runWhenDisabled;
        public bool RunWhenDisabled => runWhenDisabled;

        [BoxGroup("State")]
        [SerializeField] private bool autoDisposeOnDestroy = true;

#if UNITY_EDITOR_MODE
        private static readonly HashSet<int> warnedMissingParentNodes = new();
        private static readonly HashSet<System.Type> warnedCallbackTypes = new();
#endif

        private bool disposed;
        private bool lifecycleEntered;
        public bool IsDisposed => disposed;

        internal void MarkLifecycleEntered() =>
            lifecycleEntered = true;

        public virtual void Bind(ServiceContainer registry) { }
        public virtual void Construct(ServiceContainer registry) { }
        
        public virtual void BeforeInit() { }
        public virtual void Init() { }
        public virtual void AfterInit() { }
        
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDeltaTime) { }
        public virtual void LateTick(float deltaTime) { }

        public virtual bool IsPauseState { get; protected set; }
        public virtual void OnPauseRequest(Object owner = null) => IsPauseState = true;
        public virtual void OnResumeRequest(Object owner = null) => IsPauseState = false;

        public void Dispose()
        {
            if (disposed)
                return;
            
            if (!Application.isPlaying) 
                return;

            disposed = true;

            // Node may be present in LocalConnector list but never executed
            // (e.g. disabled LocalConnector for entire play session).
            // In that case skip DisposeInternal to avoid false null-reference teardown bugs.
            if (!lifecycleEntered)
                return;

            DisposeInternal();
        }

        protected virtual void DisposeInternal() { }

        private void OnDestroy()
        {
            if (!Application.isPlaying) 
                return;
            
            if (!autoDisposeOnDestroy) 
                return;
            
            Dispose();
        }

#if UNITY_EDITOR_MODE
        private void OnValidate()
        {
            if (Application.isPlaying)
                return;

            if (!FrameworkLogger.ShouldValidateNodeCallbacks())
                return;

            ValidateParentConnector();
            ValidateForbiddenCallbacks();
        }

        private void ValidateParentConnector()
        {
            var parentConnector = GetComponentInParent<LocalConnector>(includeInactive: true);
            var instanceId = GetInstanceID();

            if (parentConnector == null)
            {
                if (!warnedMissingParentNodes.Add(instanceId))
                    return;

                FrameworkLogger.Warning(
                    $"ConnectorNode '{GetType().Name}' on '{name}' has no parent LocalConnector. " +
                    "Attach LocalConnector first, then ConnectorNode descendants.",
                    this);

                return;
            }

            warnedMissingParentNodes.Remove(instanceId);
            parentConnector.CollectNodes();

            EditorUtility.SetDirty(parentConnector);

            if (parentConnector.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(parentConnector.gameObject.scene);
        }

        private void ValidateForbiddenCallbacks()
        {
            var type = GetType();
            if (warnedCallbackTypes.Contains(type))
                return;

            if (!HasUnityCallback(type, methodName: "Awake") &&
                !HasUnityCallback(type, methodName: "Start") &&
                !HasUnityCallback(type, methodName: "OnEnable") &&
                !HasUnityCallback(type, methodName: "OnDisable") &&
                !HasUnityCallback(type, methodName: "Update") &&
                !HasUnityCallback(type, methodName: "FixedUpdate") &&
                !HasUnityCallback(type, methodName: "LateUpdate"))
                return;

            warnedCallbackTypes.Add(type);

            FrameworkLogger.Warning(
                $"ConnectorNode '{type.Name}' declares Unity callbacks (Awake/Start/Update...). " +
                "Use AMNF lifecycle methods (Bind/Construct/Init/Tick) instead.",
                this);
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
