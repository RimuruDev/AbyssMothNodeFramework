using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    [Preserve]
    [CreateAssetMenu(menuName = "AbyssMoth/NodeFramework/Connector Debug Config", fileName = "ConnectorDebugConfig")]
    public sealed class ConnectorDebugConfig : ScriptableObject
    {
        [BoxGroup("State")]
        [SerializeField] private bool enabled = true;

        [BoxGroup("Validation"), EnableIf(nameof(enabled))]
        [SerializeField] private bool validateUnityCallbacks = true;

        [BoxGroup("Logging"), EnableIf(nameof(enabled))]
        [SerializeField] private bool logLocalConnectorExecute;

        [BoxGroup("Logging"), EnableIf(nameof(enabled))]
        [SerializeField] private bool logPhaseCalls;

        [BoxGroup("Logging"), EnableIf(nameof(enabled))]
        [SerializeField] private bool logTicks;

        [BoxGroup("Logging"), EnableIf(nameof(enabled))]
        [SerializeField] private string logTicksOnlyForConnectorName;

        public bool Enabled => enabled;
        public bool ValidateUnityCallbacks => validateUnityCallbacks;
        public bool LogLocalConnectorExecute => logLocalConnectorExecute;
        public bool LogPhaseCalls => logPhaseCalls;
        public bool LogTicks => logTicks;
        public string LogTicksOnlyForConnectorName => logTicksOnlyForConnectorName;

        public static ConnectorDebugConfig TryLoadDefault()
        {
            var config = Resources.Load<ConnectorDebugConfig>(path: "ConnectorDebugConfig");
            return config;
        }
    }
}