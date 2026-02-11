using UnityEngine;
using System.Collections.Generic;

namespace AbyssMoth
{
    public static class ConnectorDestroyUtils
    {
        private static readonly List<LocalConnector> buffer = new(capacity: 32);

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