using System;
using System.Collections.Generic;

namespace AbyssMoth
{
    public sealed class ServiceRegistry
    {
        private readonly ServiceRegistry parent;
        private readonly Dictionary<Type, object> map = new(capacity: 64);

        public ServiceRegistry(ServiceRegistry parentContainer = null) =>
            parent = parentContainer;

        public void Add<T>(T value) where T : class
        {
            var key = typeof(T);
            map[key] = value;
        }

        public bool Remove<T>() where T : class
        {
            var key = typeof(T);
            return map.Remove(key);
        }

        public bool Remove(Type type)
        {
            if (type == null)
                return false;

            return map.Remove(type);
        }
        
        public bool RemoveIfSame<T>(T expected) where T : class
        {
            var key = typeof(T);

            if (map.TryGetValue(key, out var raw) && ReferenceEquals(raw, expected))
            {
                map.Remove(key);
                return true;
            }

            return false;
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
                return parent.TryGet(out value);

            value = null;
            return false;
        }

        public T Get<T>() where T : class
        {
            if (TryGet(out T value))
                return value;

            throw new KeyNotFoundException($"Service not found: {typeof(T).Name}");
        }
        
        public bool Contains<T>() where T : class =>
            map.ContainsKey(typeof(T));

        public void AddOrThrow<T>(T value) where T : class
        {
            var key = typeof(T);

            if (map.ContainsKey(key))
                throw new InvalidOperationException($"Service already registered: {key.Name}");

            map[key] = value;
        }

        public void Clear() =>
            map.Clear();
    }
}