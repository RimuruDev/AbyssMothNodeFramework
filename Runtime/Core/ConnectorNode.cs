using UnityEngine;
using NaughtyAttributes;

namespace AbyssMoth
{
    public abstract class ConnectorNode : MonoBehaviour, 
        ILocalConnectorNode, IOrder, 
        IBind, IConstruct, IBeforeInit, IInit, IAfterInit, 
        ITick, IFixedTick, ILateTick, IPausable, IDispose
    {
        [BoxGroup("Order")]
        [SerializeField, Min(-1)] private int order;
       
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

        private bool disposed;
        public bool IsDisposed => disposed;

        public virtual void Bind(ServiceRegistry registry) { }
        public virtual void Construct(ServiceRegistry registry) { }
        
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
    }
}