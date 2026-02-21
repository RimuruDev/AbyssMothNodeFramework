using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        public static bool ShouldWarnMissingParentLocalConnectorInEditor()
        {
            var config = CurrentConfig;
            return config != null && config.WarnMissingParentLocalConnectorInEditor;
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

        public static bool ShouldCaptureInitializationTrace()
        {
            var config = CurrentConfig;
            return config != null && config.CaptureInitializationTrace;
        }

        public static bool ShouldTraceInitializationNodePhases()
        {
            var config = CurrentConfig;
            return config != null && config.CaptureInitializationTrace && config.InitializationTraceIncludeNodePhases;
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

    [Preserve]
    public static class FrameworkInitializationTrace
    {
        private const int MaxEntries = 20000;
        private const string DefaultFileNamePrefix = "AMNF_InitTrace";

        private readonly struct TraceEntry
        {
            public TraceEntry(double elapsedMs, int depth, string message)
            {
                ElapsedMs = elapsedMs;
                Depth = depth;
                Message = message;
            }

            public double ElapsedMs { get; }
            public int Depth { get; }
            public string Message { get; }
        }

        public readonly struct Scope : IDisposable
        {
            private readonly bool enabled;

            public Scope(string label, Object context = null)
            {
                enabled = BeginScopeInternal(label, context);
            }

            public void Dispose()
            {
                if (!enabled)
                    return;

                EndScopeInternal();
            }
        }

        private static readonly List<TraceEntry> entries = new(capacity: 1024);
        private static string sessionName;
        private static string lastDump;
        private static string lastSavedPath;
        private static int depth;
        private static bool sessionActive;
        private static float sessionStartRealtime;

        public static string LastSavedPath => lastSavedPath;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            entries.Clear();
            sessionName = null;
            lastDump = null;
            lastSavedPath = null;
            depth = 0;
            sessionActive = false;
            sessionStartRealtime = 0f;
        }

        public static void BeginSession(string name, Object context = null)
        {
            if (!FrameworkLogger.ShouldCaptureInitializationTrace())
                return;

            _ = context;
            entries.Clear();
            depth = 0;
            sessionActive = true;
            sessionName = string.IsNullOrWhiteSpace(name) ? "UnknownSession" : name.Trim();
            sessionStartRealtime = Time.realtimeSinceStartup;
            AddEntry($"Session Start: {sessionName}");
        }

        public static void EndSession(Object context = null)
        {
            if (!sessionActive)
                return;

            AddEntry($"Session End: {sessionName}");
            sessionActive = false;

            var dump = BuildDumpInternal();
            lastDump = dump;

            var config = FrameworkLogger.CurrentConfig;
            if (config != null)
            {
                if (config.InitializationTraceLogToConsole)
                    Debug.Log($"[AMNF] {dump}", context);

                if (config.InitializationTraceWriteToFile)
                    TryWriteDumpToFile(dump, config.InitializationTraceSubDirectory, out lastSavedPath);
            }
        }

        public static void Event(string message, Object context = null)
        {
            if (!sessionActive || string.IsNullOrWhiteSpace(message))
                return;

            _ = context;
            AddEntry(message);
        }

        public static bool TryGetLastDump(out string dump)
        {
            if (string.IsNullOrWhiteSpace(lastDump))
            {
                dump = null;
                return false;
            }

            dump = lastDump;
            return true;
        }

        public static bool TryWriteLastDumpToFile(out string filePath)
        {
            filePath = null;

            if (!TryGetLastDump(out var dump))
                return false;

            var config = FrameworkLogger.CurrentConfig;
            var subDirectory = config != null
                ? config.InitializationTraceSubDirectory
                : "AMNF/InitTrace";

            return TryWriteDumpToFile(dump, subDirectory, out filePath);
        }

        private static bool TryWriteDumpToFile(string dump, string subDirectory, out string filePath)
        {
            filePath = null;

            try
            {
                var basePath = Application.persistentDataPath;
                if (string.IsNullOrWhiteSpace(basePath))
                    return false;

                var cleanSubDir = string.IsNullOrWhiteSpace(subDirectory)
                    ? "AMNF/InitTrace"
                    : subDirectory.Trim();

                cleanSubDir = cleanSubDir.Replace("\\", "/");
                while (cleanSubDir.StartsWith("/", StringComparison.Ordinal))
                    cleanSubDir = cleanSubDir.Substring(1);

                var directory = Path.Combine(basePath, cleanSubDir);
                Directory.CreateDirectory(directory);

                var scenePart = string.IsNullOrWhiteSpace(sessionName)
                    ? "UnknownScene"
                    : SanitizeForFileName(sessionName);

                var stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff");
                var fileName = $"{DefaultFileNamePrefix}_{scenePart}_{stamp}.txt";
                var fullPath = Path.Combine(directory, fileName);

                File.WriteAllText(fullPath, dump, Encoding.UTF8);
                filePath = fullPath;
                return true;
            }
            catch (Exception e)
            {
                FrameworkLogger.Warning($"Initialization trace write failed: {e.Message}");
                return false;
            }
        }

        private static bool BeginScopeInternal(string label, Object context)
        {
            if (!sessionActive || string.IsNullOrWhiteSpace(label))
                return false;

            _ = context;
            AddEntry($"-> {label.Trim()}");
            depth++;
            return true;
        }

        private static void EndScopeInternal()
        {
            if (depth > 0)
                depth--;
        }

        private static void AddEntry(string message)
        {
            if (entries.Count >= MaxEntries)
                return;

            var elapsed = sessionStartRealtime > 0f
                ? (Time.realtimeSinceStartup - sessionStartRealtime) * 1000d
                : 0d;

            entries.Add(new TraceEntry(elapsed, depth, message));
        }

        private static string BuildDumpInternal()
        {
            var builder = new StringBuilder(capacity: Mathf.Max(entries.Count * 64, 256));

            builder.AppendLine("AMNF Initialization Trace");
            builder.Append("Session: ").AppendLine(string.IsNullOrWhiteSpace(sessionName) ? "UnknownSession" : sessionName);
            builder.Append("Entries: ").Append(entries.Count).AppendLine();
            builder.AppendLine("----------------------------------------");

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var index = i + 1;
                builder.Append('[').Append(index.ToString("0000")).Append("] ");
                builder.Append('+').Append(entry.ElapsedMs.ToString("0.000")).Append("ms ");

                for (var d = 0; d < entry.Depth; d++)
                    builder.Append("|  ");

                builder.AppendLine(entry.Message);
            }

            return builder.ToString();
        }

        private static string SanitizeForFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var builder = new StringBuilder(value.Length);

            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                var replace = false;

                for (var j = 0; j < invalid.Length; j++)
                {
                    if (invalid[j] != ch)
                        continue;

                    replace = true;
                    break;
                }

                builder.Append(replace ? '_' : ch);
            }

            return builder.ToString();
        }
    }
}
