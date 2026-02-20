using UnityEngine;
using System.Collections.Generic;
using Object = UnityEngine.Object;

namespace AbyssMoth
{
    public static class ConnectorDestroyUtils
    {
        private static readonly List<LocalConnector> buffer = new(capacity: 32);

        public static void DisposeAndDestroy(this ConnectorNode target)
        {
            if (target == null)
                return;

            DisposeAndDestroy(target.gameObject);
        }

        public static void DisposeAndDestroy(this MonoBehaviour target)
        {
            if (target == null)
                return;

            DisposeAndDestroy(target.gameObject);
        }

        public static void DisposeAndDestroy(this Object target)
        {
            if (target == null)
                return;

            if (target is GameObject go)
            {
                DisposeAndDestroy(go);
                return;
            }

            if (target is Component component)
            {
                DisposeAndDestroy(component.gameObject);
                return;
            }

            Object.Destroy(target);
        }

        public static void DisposeAndDestroy(GameObject target)
        {
            if (target == null)
                return;

            buffer.Clear();
            target.GetComponentsInChildren(includeInactive: true, results: buffer);

            for (var i = 0; i < buffer.Count; i++)
            {
                var c = buffer[i];

                if (c != null)
                    c.Dispose();
            }

            buffer.Clear();

            Object.Destroy(target);
        }
    }
}
