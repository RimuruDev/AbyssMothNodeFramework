using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public abstract class ConnectorConstructInitNode : ConnectorInitNode, IConstruct
    {
        public virtual void Construct(ServiceRegistry registry) { }
    }
}