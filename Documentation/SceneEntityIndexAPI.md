# SceneEntityIndex API (RU)

- Актуально для ветки пакета: `v3.x` (текущая сборка `3.0.0`)

Краткий справочник по методам `SceneEntityIndex`.

## Получение индекса

```csharp
var index = registry.Get<SceneEntityIndex>();
```

---

## Регистрация и обслуживание

### `Register(LocalConnector connector)`
Регистрирует `LocalConnector` в индексах (id/tag/type/nodes).

```csharp
index.Register(localConnector);
```

### `Unregister(LocalConnector connector)`
Убирает `LocalConnector` из всех индексов.

```csharp
index.Unregister(localConnector);
```

### `Refresh(LocalConnector connector)`
Переиндексирует уже зарегистрированный `LocalConnector` (например, после смены `EntityTag`).

```csharp
localConnector.SetEntityTag("Boss");
index.Refresh(localConnector);
```

### `Clear()`
Полная очистка всех внутренних карт индекса.

```csharp
index.Clear();
```

### `PruneDeadReferences()`
Удаляет уничтоженные ссылки из внутренних коллекций.

```csharp
index.PruneDeadReferences();
```

### `BuildDump(int maxItemsPerGroup = 40)`
Строит текстовый дамп состояния индекса.

```csharp
Debug.Log(index.BuildDump());
```

---

## Поиск по Id и Tag

### `TryGetById(int id, out LocalConnector connector)`

```csharp
if (index.TryGetById(10, out var connector))
{
}
```

### `GetByIdOrThrow(int id)`

```csharp
var connector = index.GetByIdOrThrow(10);
```

### `TryGetFirstByTag(string tag, out LocalConnector connector)`

```csharp
if (index.TryGetFirstByTag("Hero", out var hero))
{
}
```

### `TryGetByTag(string tag, out LocalConnector connector)`
Alias для `TryGetFirstByTag`.

### `TryGetFirstByTag<TConnector>(string tag, out TConnector connector, bool includeDerived = true)`

```csharp
if (index.TryGetFirstByTag<LocalConnector>("Enemy", out var enemy))
{
}
```

### `GetAllByTag(string tag)`

```csharp
var allEnemies = index.GetAllByTag("Enemy");
```

### `GetAllByTagNonAlloc(string tag, List<LocalConnector> buffer)`

```csharp
var buffer = new List<LocalConnector>(32);
var count = index.GetAllByTagNonAlloc("Enemy", buffer);
```

### `GetFirstByTagOrThrow(string tag)`

```csharp
var hero = index.GetFirstByTagOrThrow("Hero");
```

---

## Поиск по типу коннектора

### `TryGetFirstConnector<TConnector>(out TConnector connector, bool includeDerived = true)`

```csharp
if (index.TryGetFirstConnector<MyLocalConnector>(out var connector))
{
}
```

### `GetConnectors<TConnector>(List<TConnector> buffer, bool includeDerived = true)`

```csharp
var buffer = new List<MyLocalConnector>(16);
var count = index.GetConnectors(buffer, includeDerived: true);
```

---

## Поиск по типу ноды

### `TryGetFirstNode<T>(out T node, bool includeDerived = true)`

```csharp
if (index.TryGetFirstNode<MyNode>(out var node))
{
}
```

### `FindFirstNode<T>(bool includeDerived = true)`

```csharp
var node = index.FindFirstNode<MyNode>();
```

### `GetNodes<T>(List<T> buffer, bool includeDerived = true)`

```csharp
var buffer = new List<MyNode>(32);
var count = index.GetNodes(buffer);
```

### `GetFirstNodeOrThrow<T>(bool includeDerived = true)`

```csharp
var node = index.GetFirstNodeOrThrow<MyNode>();
```

---

## Поиск ноды внутри сущности

### `TryGetNodeInConnector<T>(LocalConnector connector, out T node)`

```csharp
if (index.TryGetNodeInConnector<HealthNode>(heroConnector, out var health))
{
}
```

### `TryGetNodeInFirstByTag<T>(string tag, out T node)`

```csharp
if (index.TryGetNodeInFirstByTag<HealthNode>("Hero", out var health))
{
}
```

---

## Служебные методы и счетчики

### Счетчики
- `RegisteredCount`
- `IdCount`
- `TagCount`
- `ConnectorTypeCount`
- `NodeTypeCount`

### Внутренний boot/runtime utility
- `PrimeReservedIds(...)` — резервирует id для статических коннекторов перед runtime-спавном.
- `AllocateId()` — выдает новый id с учетом уже занятых и зарезервированных.

Обычно эти методы вызываются инфраструктурой (`SceneConnector`) автоматически.
