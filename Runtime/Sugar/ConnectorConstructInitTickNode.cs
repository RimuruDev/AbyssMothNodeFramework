using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public abstract class ConnectorConstructInitTickNode : ConnectorConstructInitNode, ITick, IFixedTick, ILateTick
    {
        public virtual void Tick(float deltaTime) { }
        public virtual void FixedTick(float fixedDeltaTime) { }
        public virtual void LateTick(float deltaTime) { }
    }
}