# Connector Lifecycle And Node Templates (RU)

- Актуально для ветки пакета: `v3.x`

## 1) Минимальный шаблон ноды

```csharp
using UnityEngine;

namespace AbyssMothNodeFramework.Example
{
    public sealed class MyFeatureNode : ConnectorNode
    {
        public override void Bind(ServiceContainer registry)
        {
            // Container уже доступен из base ConnectorNode
        }

        public override void Construct(ServiceContainer registry)
        {
            // Получение зависимостей
            // var index = SceneEntityIndex;
            // var scene = SceneConnector;
            // var lifecycle = AppLifecycle;
        }

        public override void Init()
        {
            // Старт фичи
        }

        public override void Tick(float deltaTime)
        {
            // Runtime шаг
        }

        protected override void DisposeInternal()
        {
            // Отписки / cleanup
        }
    }
}
```

## 2) Где что делать

- `Bind`:
  - зарегистрировать сервисы этой ноды.
- `Construct`:
  - получить обязательные зависимости,
  - подписаться на события.
- `BeforeInit/Init/AfterInit`:
  - фазовая инициализация фичи.
- `Tick/FixedTick/LateTick`:
  - игровая логика по кадрам.
- `DisposeInternal`:
  - отписки и освобождение ресурсов.

## 3) Удобный доступ из `ConnectorNode` (без ручного кеша registry)

Доступно прямо из `this`:
- `Container` (`ServiceContainer`)
- `OwnerConnector` (`LocalConnector`)
- `SceneConnector` (`SceneConnector`)
- `SceneEntityIndex` (`SceneEntityIndex`)
- `AppLifecycle` (`AppLifecycleService`)

Shortcut методы:
- `GetService<T>()`
- `TryGetService<T>(out T value)`

## 4) Если не уверен, что зависимость уже в контейнере

Безопасный вариант:
- попытка через `TryGet` в `Construct`,
- финальная инициализация в `AfterInit`.

```csharp
public override void Construct(ServiceContainer registry)
{
    TryGetService(out SceneEntityIndex _);
}

public override void AfterInit()
{
    var index = GetService<SceneEntityIndex>();
}
```

## 5) Порядок выполнения

`Order` влияет на порядок внутри одного `LocalConnector`.

Для installer-узлов обычно ставят отрицательное значение (`-1000 .. -1`), чтобы они шли раньше обычной логики.

## 6) Про Unity callbacks

Для `ConnectorNode` рекомендуется не использовать `Awake/Start/Update`.
Используй lifecycle фреймворка (`Bind/Construct/Init/Tick`).
