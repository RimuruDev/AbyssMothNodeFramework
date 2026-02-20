using UnityEngine;

namespace AbyssMoth
{
    public interface ILocalConnectorNode { }

    public interface IOrder
    {
        public int Order { get; }
    }

    public interface IBind
    {
        public void Bind(ServiceContainer registry);
    }

    public interface IConstruct
    {
        public void Construct(ServiceContainer registry);
    }

    public interface IPausable
    {
        public bool IsPauseState { get; }
        public void OnPauseRequest(Object owner = null);
        public void OnResumeRequest(Object owner = null);
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