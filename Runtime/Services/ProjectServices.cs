using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    public static class ProjectServices
    {
        public static ServiceContainer Context =>
            ProjectRootRegistry.GetContext();

        public static FrameworkConfig Config
        {
            get
            {
                if (Context.TryGet(out FrameworkConfig config))
                    return config;

                return FrameworkConfig.TryLoadDefault();
            }
        }
        
        public static T Get<T>() where T : class =>
            Context.Get<T>();

        public static bool TryGet<T>(out T value) where T : class =>
            Context.TryGet(out value);

        public static void Add<T>(T value) where T : class =>
            Context.Add(value);
    }
}
