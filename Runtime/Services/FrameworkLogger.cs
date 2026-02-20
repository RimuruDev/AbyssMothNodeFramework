using UnityEngine;
using UnityEngine.Scripting;
using Object = UnityEngine.Object;

namespace AbyssMoth
{
    [Preserve]
    public static class FrameworkLogger
    {
        private const string Prefix = "[AMNF]";

        private static FrameworkConfig configured;
        private static FrameworkConfig cachedLoaded;
        private static bool attemptedLoad;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            configured = null;
            cachedLoaded = null;
            attemptedLoad = false;
        }

        public static void Configure(FrameworkConfig config)
        {
            configured = config;

            if (config != null)
            {
                cachedLoaded = config;
                attemptedLoad = true;
            }
        }

        public static FrameworkConfig CurrentConfig
        {
            get
            {
                if (configured != null)
                    return configured;

                if (attemptedLoad)
                    return cachedLoaded;

                cachedLoaded = FrameworkConfig.TryLoadDefault();
                attemptedLoad = true;
                return cachedLoaded;
            }
        }

        public static bool CanLog(FrameworkLogLevel level)
        {
            var config = CurrentConfig;
            if (config == null)
                return level <= FrameworkLogLevel.Warning && level != FrameworkLogLevel.None;

            if (!config.EnableFrameworkLogs)
                return false;

            return level <= config.MinimumLogLevel && level != FrameworkLogLevel.None;
        }

        public static void Info(string message, Object context = null) =>
            Log(FrameworkLogLevel.Info, message, context);

        public static void Verbose(string message, Object context = null) =>
            Log(FrameworkLogLevel.Verbose, message, context);

        public static void Warning(string message, Object context = null) =>
            Log(FrameworkLogLevel.Warning, message, context);

        public static void Error(string message, Object context = null) =>
            Log(FrameworkLogLevel.Error, message, context);

        public static void Boot(string message, Object context = null)
        {
            var config = CurrentConfig;
            if (config == null || !config.LogBootSequence)
                return;

            Info(message, context);
        }

        public static bool ShouldValidateNodeCallbacks()
        {
            var config = CurrentConfig;
            return config == null || config.ValidateNodeUnityCallbacks;
        }

        public static bool ShouldLogConnectorExecute()
        {
            var config = CurrentConfig;
            return config != null && config.LogConnectorExecute && CanLog(FrameworkLogLevel.Info);
        }

        public static bool ShouldLogNodePhases()
        {
            var config = CurrentConfig;
            return config != null && config.LogNodePhases && CanLog(FrameworkLogLevel.Verbose);
        }

        public static bool ShouldLogTick(string connectorName)
        {
            var config = CurrentConfig;

            if (config == null || !config.LogTickCalls || !CanLog(FrameworkLogLevel.Verbose))
                return false;

            var filter = config.LogTicksOnlyForConnectorName;
            if (string.IsNullOrWhiteSpace(filter))
                return true;

            return string.Equals(filter.Trim(), connectorName, System.StringComparison.Ordinal);
        }

        private static void Log(FrameworkLogLevel level, string message, Object context)
        {
            if (!CanLog(level))
                return;

            var formatted = $"{Prefix} {message}";

            switch (level)
            {
                case FrameworkLogLevel.Error:
                    Debug.LogError(formatted, context);
                    return;
                case FrameworkLogLevel.Warning:
                    Debug.LogWarning(formatted, context);
                    return;
                default:
                    Debug.Log(formatted, context);
                    return;
            }
        }
    }
}
