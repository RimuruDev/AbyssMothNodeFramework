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

## 3) FrameworkLogger API

```csharp
FrameworkLogger.Info("message", this);
FrameworkLogger.Warning("message", this);
FrameworkLogger.Error("message", this);
FrameworkLogger.Verbose("message", this);
FrameworkLogger.Boot("boot message", this);
```

Логи проходят через фильтры `FrameworkConfig`.

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
