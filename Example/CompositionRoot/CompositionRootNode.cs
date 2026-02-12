using System.Diagnostics.CodeAnalysis;
using AbyssMoth;

namespace AbyssMothNodeFramework.Example
{
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    public class CompositionRootNode : ConnectorNodeBehaviour
    {
        private ServiceRegistry container;

#if UNITY_EDITOR
        private void OnValidate()
        {
            Order = -1;

            if (transform.parent.GetComponent<LocalConnector>() == null && GetComponent<LocalConnector>() == null)
                gameObject.AddComponent<LocalConnector>();
        }
#endif

        public override void Bind(ServiceRegistry registry)
        {
            container = registry;
            
            // Initialize 
            // Spawn core gameplay stuff 
            
            // Register
            // Resolve 
            // Dispose
        }

        public override void BeforeInit()
        {
            // Awake (Initialization - 1/3)
            // Get/Subscribe
        }

        public override void Init()
        {
            // Start (Initialization - 2/3)
            // Validate/Spawn/Ets
        }

        public override void AfterInit()
        {
           // End (Initialization - 3/3)
           // 100% - Warmed up !!!
        }

        protected override void DisposeInternal()
        {
            // Dispose subscribers:
            // container.RemoveIfSame(expected: null);
        }
    }
}