using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public abstract class ConnectorInitNode : ConnectorNode, IBeforeInit, IInit, IAfterInit, IDispose
    {
        [BoxGroup("State")]
        [SerializeField] private bool autoDisposeOnDestroy = true;

        private bool disposed;

        public bool IsDisposed => disposed;

        public virtual void BeforeInit() { }
        public virtual void Init() { }
        public virtual void AfterInit() { }

        public void Dispose()
        {
            if (disposed)
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