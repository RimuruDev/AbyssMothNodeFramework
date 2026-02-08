using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public abstract class ConnectorInitNode : ConnectorNode, IBeforeInit, IInit, IAfterInit, IDispose
    {
        public virtual void BeforeInit() { }
        public virtual void Init() { }
        public virtual void AfterInit() { }
        public virtual void Dispose() { }
    }
}