using System;
using System.Collections.Generic;

namespace AbyssMoth
{
    public sealed class ServiceContainer
    {
        private readonly ServiceContainer parent;
        private readonly Dictionary<Type, object> map = new(capacity: 64);
        private readonly Dictionary<(Type type, string tag), object> taggedMap = new(capacity: 32);

        public ServiceContainer(ServiceContainer parentContainer = null) =>
            parent = parentContainer;

        public void Add<T>(T value) where T : class
        {
            var key = typeof(T);
            map[key] = value;
        }

        public void AddSingle<T>(T value) where T : class =>
            AddOrThrow(value);

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

        public void AddTagged<T>(string tag, T value, bool overwrite = true) where T : class
        {
            if (string.IsNullOrWhiteSpace(tag))
                throw new ArgumentException("Tag cannot be null or whitespace.", nameof(tag));

            var key = (typeof(T), tag.Trim());

            if (!overwrite && taggedMap.ContainsKey(key))
                throw new InvalidOperationException($"Tagged service already registered: {key.Item1.Name}[{key.Item2}]");

            taggedMap[key] = value;
        }

        public bool RemoveTagged<T>(string tag) where T : class
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            return taggedMap.Remove((typeof(T), tag.Trim()));
        }

        public bool TryResolve<T>(out T value) where T : class =>
            TryGet(out value);

        public T Resolve<T>() where T : class =>
            Get<T>();

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

        public bool TryGetTagged<T>(string tag, out T value) where T : class
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                value = null;
                return false;
            }

            var key = (typeof(T), tag.Trim());

            if (taggedMap.TryGetValue(key, out var raw) && raw is T typed)
            {
                value = typed;
                return true;
            }

            if (parent != null)
                return parent.TryGetTagged(tag, out value);

            value = null;
            return false;
        }

        public T GetTagged<T>(string tag) where T : class
        {
            if (TryGetTagged(tag, out T value))
                return value;

            throw new KeyNotFoundException($"Tagged service not found: {typeof(T).Name}[{tag}]");
        }

        public T Get<T>() where T : class
        {
            if (TryGet(out T value))
                return value;

            throw new KeyNotFoundException($"Service not found: {typeof(T).Name}");
        }

        public bool Contains<T>() where T : class =>
            map.ContainsKey(typeof(T));

        public bool ContainsTagged<T>(string tag) where T : class
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            return taggedMap.ContainsKey((typeof(T), tag.Trim()));
        }

        public void AddOrThrow<T>(T value) where T : class
        {
            var key = typeof(T);

            if (map.ContainsKey(key))
                throw new InvalidOperationException($"Service already registered: {key.Name}");

            map[key] = value;
        }

        public void Clear()
        {
            map.Clear();
            taggedMap.Clear();
        }
    }
}
