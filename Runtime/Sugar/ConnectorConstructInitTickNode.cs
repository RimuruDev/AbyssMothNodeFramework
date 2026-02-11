using UnityEngine;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public abstract class ConnectorConstructInitTickNode : ConnectorConstructInitNode, ITick, IFixedTick, ILateTick, IPausable
    {
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDeltaTime) { }
        public virtual void LateTick(float deltaTime) { }
        
        public virtual bool IsPauseState { get; private set; }
        public virtual void OnPauseRequest(Object owner = null) => IsPauseState = true;
        public virtual void OnResumeRequest(Object owner= null) => IsPauseState = false;
    }
}