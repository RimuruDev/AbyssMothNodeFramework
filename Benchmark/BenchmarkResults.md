# Benchmark Results (Historical)

Источник: исторические логи `ConnectorBenchmarkRunner` из старой версии файла.

## Срез 1

- Baseline double-loop: `59.666 ms` | `10,000,000 calls` | `~5.97 ns/call`
- Interface list Step: `30.292 ms` | `10,000,000 calls` | `~3.03 ns/call`
- LocalConnector Tick (cached): `182.26 ms` | `10,000,000 calls` | `~18.23 ns/call`

## Срез 2

- Baseline double-loop: `54.414 ms` | `10,000,000 calls` | `~5.44 ns/call`
- Interface list Step: `29.201 ms` | `10,000,000 calls` | `~2.92 ns/call`
- LocalConnector Tick (cached): `177.095 ms` | `10,000,000 calls` | `~17.71 ns/call`

## Unity probe

- Unity BehaviourUpdate avg: `0.179 ms/frame` (`frames: 300`)

## Вывод из старого прогона

Оценка накладных расходов диспетчеризации (`LocalConnector.Tick` vs interface-loop):
- порядка `~15 ns` на вызов в том замере.
- при `1000` тикающих нод это около `0.015 ms/frame`.
- при `5000` тикающих нод это около `0.075 ms/frame`.

Важно: эти числа исторические. Для актуальной версии использовать свежие прогоны по протоколу из `Benchmark/README.md`.
