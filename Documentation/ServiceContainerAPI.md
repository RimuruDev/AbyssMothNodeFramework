# ServiceContainer API (RU)

- Актуально для ветки пакета: `v3.x`

`ServiceContainer` — DI-контейнер с поддержкой parent-контекста и tagged-сервисов.

## Основные методы

### Регистрация

```csharp
registry.Add(service);
registry.AddSingle(service);   // AddOrThrow-стиль
registry.AddOrThrow(service);
```

### Получение

```csharp
var service = registry.Get<MyService>();
var ok = registry.TryGet(out MyService service2);

var tagged = registry.GetTagged<MyService>("Hero");
var okTagged = registry.TryGetTagged("Hero", out MyService tagged2);
```

### Проверка

```csharp
registry.Contains<MyService>();
registry.ContainsTagged<MyService>("Hero");
```

### Удаление

```csharp
registry.Remove<MyService>();
registry.Remove(typeof(MyService));
registry.RemoveIfSame(expectedInstance);
registry.RemoveTagged<MyService>("Hero");
```

### Tagged регистрация

```csharp
registry.AddTagged<MyService>("Hero", service, overwrite: true);
```

Если `overwrite: false` и тег уже занят, кидается `InvalidOperationException`.

## Parent-context

Если сервиса нет в текущем контейнере, `Get/TryGet` автоматически поднимаются в parent.

Это позволяет `SceneContext` видеть сервисы из `ProjectContext`.

## Практический паттерн

```csharp
public override void Bind(ServiceContainer registry)
{
    if (!registry.Contains<MyFeatureConfig>())
        registry.Add(new MyFeatureConfig());
}

protected override void DisposeInternal()
{
    registry.Remove<MyFeatureConfig>();
}
```
