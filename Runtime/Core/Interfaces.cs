namespace AbyssMoth
{
    public interface ILocalConnectorNode { }

    public interface IConnectorOrder
    {
        public int Order { get; }
    }
    
    public interface ILocalConnectorOrder
    {
        public int Order { get; }
    }

    public interface IBind
    {
        public void Bind(ServiceRegistry registry);
    }

    public interface IConstruct
    {
        public void Construct(ServiceRegistry registry);
    }

    public interface IBeforeInit
    {
        public void BeforeInit();
    }

    public interface IInit
    {
        public void Init();
    }

    public interface IAfterInit
    {
        public void AfterInit();
    }

    public interface ITick
    {
        public void Tick(float deltaTime);
    }

    public interface IFixedTick
    {
        public void FixedTick(float fixedDeltaTime);
    }

    public interface ILateTick
    {
        public void LateTick(float deltaTime);
    }

    public interface IDispose
    {
        public void Dispose();
    }
}