using System;
using System.Collections.Generic;

namespace AbyssMoth
{
    public sealed class ServiceRegistry
    {
        private readonly ServiceRegistry parent;
        private readonly Dictionary<Type, object> map = new(64);

        public ServiceRegistry(ServiceRegistry parentContainer = null)
        {
            parent = parentContainer;
        }

        public void Add<T>(T value) where T : class
        {
            var key = typeof(T);
            map[key] = value;
        }

        public bool TryGet<T>(out T value) where T : class
        {
            var key = typeof(T);

            if (map.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            if (parent != null)
            {
                return parent.TryGet(out value);
            }

            value = null;
            return false;
        }

        public T Get<T>() where T : class
        {
            if (TryGet<T>(out var value))
            {
                return value;
            }

            throw new KeyNotFoundException($"Service not found: {typeof(T).Name}");
        }
    }
}   