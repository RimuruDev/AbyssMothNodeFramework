# AMNF Benchmark

Назначение: быстрый sanity-check стоимости диспетчеризации `Tick` в `LocalConnector`.

## Что измеряет

`ConnectorBenchmarkRunner` сравнивает:
- baseline double-loop,
- вызов интерфейса (`IBenchmarkStep.Step`),
- `LocalConnector.Tick` с кэшированными нодами,
- (опционально) `BehaviourUpdate` через `ProfilerRecorder`.

## Как запускать

1. Открой пустую сцену в Editor.
2. Добавь `ConnectorBenchmarkRunner` на объект сцены.
3. Включи Play Mode.
4. Нажми кнопку `Run Benchmark` в инспекторе.
5. Скопируй лог из Console.

## Рекомендации по чистоте замера

- не держать тяжелые системы в сцене,
- отключить лишние editor окна/оверлеи,
- запускать несколько прогонов и смотреть медиану,
- сравнивать относительные изменения между версиями, а не абсолютные ns.

## Параметры по умолчанию

- `nodesCount = 1000`
- `tickLoops = 10000`
- `warmupLoops = 300`

Итого ~10M вызовов на замер.

## Исторические результаты

Старые результаты вынесены в:
- `Benchmark/BenchmarkResults.md`
