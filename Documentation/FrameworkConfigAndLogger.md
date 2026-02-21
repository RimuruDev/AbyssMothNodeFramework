# FrameworkConfig And Logger (RU)

- Актуально для ветки пакета: `v3.x`

## 1) Где лежит конфиг

Обычно конфиг создается в:

`Assets/AbyssMothNodeFramework/Resources/AbyssMothNodeFramework/FrameworkConfig.asset`

Быстрое создание:
- `Edit -> AbyssMoth Node Framework -> Initialize Project`
- или `Assets/Create/AbyssMoth/NodeFramework/Framework Config`

## 2) Что настраивает FrameworkConfig

### Boot
- `ApplyBootstrapSettings`
- `OverrideTargetFrameRate` / `TargetFrameRate`
- `OverrideVSyncCount` / `VSyncCount`
- `SleepTimeoutMode`

### Services
- `RegisterDefaultSceneTransitionService`
- `DefaultTransitionSceneName`

### Diagnostics
- `EnableFrameworkLogs`
- `MinimumLogLevel`
- `LogBootSequence`
- `LogConnectorExecute`
- `LogNodePhases`
- `LogTickCalls`
- `LogTicksOnlyForConnectorName`
- `ValidateNodeUnityCallbacks`
- `WarnMissingParentLocalConnectorInEditor` (`false` по умолчанию)

### Diagnostics / Initialization Trace
- `CaptureInitializationTrace` — включает сбор дерева инициализации.
- `InitializationTraceLogToConsole` — выводит весь граф одной пачкой в консоль после загрузки сцены.
- `InitializationTraceWriteToFile` — сохраняет `.txt` в `Application.persistentDataPath`.
- `InitializationTraceSubDirectory` — подпапка внутри `persistentDataPath`.
- `InitializationTraceIncludeNodePhases` — добавляет фазы `Bind/Construct/BeforeInit/Init/AfterInit` по нодам.

## 3) FrameworkLogger API

```csharp
FrameworkLogger.Info("message", this);
FrameworkLogger.Warning("message", this);
FrameworkLogger.Error("message", this);
FrameworkLogger.Verbose("message", this);
FrameworkLogger.Boot("boot message", this);
```

Логи проходят через фильтры `FrameworkConfig`.

Для графа инициализации:
- `Edit -> AbyssMoth Node Framework -> Diagnostics -> Copy Last Init Trace To Clipboard`
- `Edit -> AbyssMoth Node Framework -> Diagnostics -> Save Last Init Trace To File`

## 4) Рекомендованный режим для продакшена

- `EnableFrameworkLogs = true`
- `MinimumLogLevel = Warning`
- `LogTickCalls = false`
- `LogNodePhases = false`

## 5) Рекомендованный режим для диагностики startup

- `MinimumLogLevel = Verbose`
- `LogBootSequence = true`
- `LogConnectorExecute = true`
- `LogNodePhases = true`
- `CaptureInitializationTrace = true`
- `InitializationTraceLogToConsole = true`
- `InitializationTraceWriteToFile = true`
