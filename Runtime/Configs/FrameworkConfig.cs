using UnityEngine;
using NaughtyAttributes;
using UnityEngine.Scripting;

namespace AbyssMoth
{
    public enum FrameworkLogLevel
    {
        None = 0,
        Error = 1,
        Warning = 2,
        Info = 3,
        Verbose = 4,
    }

    public enum FrameworkSleepTimeoutMode
    {
        KeepCurrent = 0,
        SystemSetting = 1,
        NeverSleep = 2,
    }

    [Preserve]
    [CreateAssetMenu(menuName = "AbyssMoth/NodeFramework/Framework Config", fileName = "FrameworkConfig")]
    public sealed class FrameworkConfig : ScriptableObject
    {
        [BoxGroup("Boot")]
        [SerializeField] private bool applyBootstrapSettings = true;

        [BoxGroup("Boot"), EnableIf(nameof(applyBootstrapSettings))]
        [SerializeField] private bool overrideTargetFrameRate = true;

        [BoxGroup("Boot"), EnableIf(nameof(overrideTargetFrameRate))]
        [SerializeField] private int targetFrameRate = 60;

        [BoxGroup("Boot"), EnableIf(nameof(applyBootstrapSettings))]
        [SerializeField] private bool overrideVSyncCount = true;

        [BoxGroup("Boot"), EnableIf(nameof(overrideVSyncCount))]
        [SerializeField, Range(0, 4)] private int vSyncCount;

        [BoxGroup("Boot"), EnableIf(nameof(applyBootstrapSettings))]
        [SerializeField] private FrameworkSleepTimeoutMode sleepTimeoutMode = FrameworkSleepTimeoutMode.NeverSleep;

        [BoxGroup("Services")]
        [SerializeField] private bool registerDefaultSceneTransitionService;

        [BoxGroup("Services"), EnableIf(nameof(registerDefaultSceneTransitionService))]
        [SerializeField] private string defaultTransitionSceneName = Constants.EmptySceneTransitionName;

        [BoxGroup("Diagnostics")]
        [SerializeField] private bool enableFrameworkLogs = true;

        [BoxGroup("Diagnostics"), EnableIf(nameof(enableFrameworkLogs))]
        [SerializeField] private FrameworkLogLevel minimumLogLevel = FrameworkLogLevel.Warning;

        [BoxGroup("Diagnostics"), EnableIf(nameof(enableFrameworkLogs))]
        [SerializeField] private bool logBootSequence;

        [BoxGroup("Diagnostics"), EnableIf(nameof(enableFrameworkLogs))]
        [SerializeField] private bool logConnectorExecute;

        [BoxGroup("Diagnostics"), EnableIf(nameof(enableFrameworkLogs))]
        [SerializeField] private bool logNodePhases;

        [BoxGroup("Diagnostics"), EnableIf(nameof(enableFrameworkLogs))]
        [SerializeField] private bool logTickCalls;

        [BoxGroup("Diagnostics"), EnableIf(nameof(logTickCalls))]
        [SerializeField] private string logTicksOnlyForConnectorName;

        [BoxGroup("Diagnostics/Initialization Trace")]
        [SerializeField] private bool captureInitializationTrace;

        [BoxGroup("Diagnostics/Initialization Trace"), EnableIf(nameof(captureInitializationTrace))]
        [SerializeField] private bool initializationTraceLogToConsole = true;

        [BoxGroup("Diagnostics/Initialization Trace"), EnableIf(nameof(captureInitializationTrace))]
        [SerializeField] private bool initializationTraceWriteToFile;

        [BoxGroup("Diagnostics/Initialization Trace"), EnableIf(nameof(initializationTraceWriteToFile))]
        [SerializeField] private string initializationTraceSubDirectory = "AMNF/InitTrace";

        [BoxGroup("Diagnostics/Initialization Trace"), EnableIf(nameof(captureInitializationTrace))]
        [SerializeField] private bool initializationTraceIncludeNodePhases = true;

        [BoxGroup("Diagnostics")]
        [SerializeField] private bool validateNodeUnityCallbacks = true;

        [BoxGroup("Diagnostics/Editor")]
        [SerializeField] private bool warnMissingParentLocalConnectorInEditor;

        public bool ApplyBootstrapSettings => applyBootstrapSettings;
        public bool OverrideTargetFrameRate => overrideTargetFrameRate;
        public int TargetFrameRate => targetFrameRate;
        public bool OverrideVSyncCount => overrideVSyncCount;
        public int VSyncCount => vSyncCount;
        public FrameworkSleepTimeoutMode SleepTimeoutMode => sleepTimeoutMode;

        public bool RegisterDefaultSceneTransitionService => registerDefaultSceneTransitionService;
        public string DefaultTransitionSceneName => defaultTransitionSceneName;

        public bool EnableFrameworkLogs => enableFrameworkLogs;
        public FrameworkLogLevel MinimumLogLevel => minimumLogLevel;
        public bool LogBootSequence => logBootSequence;
        public bool LogConnectorExecute => logConnectorExecute;
        public bool LogNodePhases => logNodePhases;
        public bool LogTickCalls => logTickCalls;
        public string LogTicksOnlyForConnectorName => logTicksOnlyForConnectorName;

        public bool CaptureInitializationTrace => captureInitializationTrace;
        public bool InitializationTraceLogToConsole => initializationTraceLogToConsole;
        public bool InitializationTraceWriteToFile => initializationTraceWriteToFile;
        public string InitializationTraceSubDirectory => initializationTraceSubDirectory;
        public bool InitializationTraceIncludeNodePhases => initializationTraceIncludeNodePhases;

        public bool ValidateNodeUnityCallbacks => validateNodeUnityCallbacks;
        public bool WarnMissingParentLocalConnectorInEditor => warnMissingParentLocalConnectorInEditor;

        public int ResolveSleepTimeoutValue()
        {
            return sleepTimeoutMode switch
            {
                FrameworkSleepTimeoutMode.SystemSetting => SleepTimeout.SystemSetting,
                FrameworkSleepTimeoutMode.NeverSleep => SleepTimeout.NeverSleep,
                _ => int.MinValue,
            };
        }

        public static FrameworkConfig TryLoadDefault()
        {
            var config = Resources.Load<FrameworkConfig>(path: NodeFrameworkPaths.FrameworkConfig);
            if (config != null)
                return config;

            config = Resources.Load<FrameworkConfig>(path: NodeFrameworkPaths.PackageFrameworkConfig);
            if (config != null)
                return config;

            return Resources.Load<FrameworkConfig>(path: NodeFrameworkPaths.LegacyFrameworkConfig);
        }
    }
}
