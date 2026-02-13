# AbyssMothNodeFramework

## **Dependencies: NaugntyAttribute** ->  [assetstore](https://assetstore.unity.com/packages/tools/utilities/naughtyattributes-129996)


<img width="1536" height="1024" alt="Logo" src="https://github.com/user-attachments/assets/f9c72ff8-bb79-44a8-afef-f1bed5ca4e0e" />

# AbyssMoth NodeFramework — Cookbook (v2.2.4)

## 0) Старт игры: кто кого запускает

**Boot цепочка:**
1) `AppEntry` создаёт `GlobalRoot` и вешает `SceneOrchestrator` (DontDestroyOnLoad)
2) `SceneOrchestrator` поднимает `ProjectRootConnector` (из Resources, если надо)
3) `ProjectRootConnector.Awake()` создаёт `ProjectContext` и вызывает `Execute(ProjectContext, sender: this)`
4) На каждый `sceneLoaded` `SceneOrchestrator` вызывает `SceneConnector.Execute(projectRoot.ProjectContext)` для сцены

---

## 1) Контейнеры: где какие сервисы живут

### ProjectContext (глобальный, живёт всегда)
- Хост: `ProjectRootConnector.ProjectContext`
- Доступ откуда угодно:
  - `ProjectRootRegistry.GetContext()`
  - `ProjectServices.Context`
  - `ProjectServices.Get<T>() / TryGet<T>() / Add<T>()`

### SceneContext (на сцену, родитель = ProjectContext)
- Хост: `SceneConnector.SceneContext`
- Создаётся внутри `SceneConnector.Execute(projectContext)` как `new ServiceRegistry(parentContainer: projectContext)`
- Внутри сцены любой нод получает `ServiceRegistry registry` в `Bind/Construct/...` — это и есть SceneContext

### Важно
- У `ServiceRegistry` нет публичного доступа к parent — и не надо: `TryGet/Get` автоматически поднимаются вверх.
- Если ты в SceneContext вызываешь `registry.Get<SomeGlobalService>()`, он спокойно найдёт его в ProjectContext.

---

## 2) ServiceRegistry: как регать/получать сервисы

### Внутри нода (у тебя есть `registry`)
```csharp
public sealed class AudioBootstrapNode : ConnectorNode
{
    public override void Bind(ServiceRegistry registry)
    {
        registry.Add(new AudioService());
    }

    protected override void DisposeInternal()
    {
        // если надо гарантированно убрать именно свой инстанс:
        // registry.RemoveIfSame(expected: audioService);
    }
}
````

### Глобально (ProjectContext) из любого места

```csharp
var lifecycle = ProjectServices.Get<AppLifecycleService>();
ProjectServices.Add(new AnalyticsService());
var analytics = ProjectServices.Get<AnalyticsService>();
```

### Когда нужен TryGet, а когда Get

* `TryGet` — если сервис опциональный / модульный
* `Get` — если сервис “обязан существовать” (например аналитика в сборке с аналитикой)

---

## 3) Жизненный цикл нода (ConnectorNode)

### Порядок вызовов при Execute(LocalConnector)

1. `Bind(registry)`
2. `Construct(registry)`
3. `BeforeInit()`
4. `Init()`
5. `AfterInit()`
6. затем циклы: `Tick / FixedTick / LateTick`

### Где что делать

* `Bind` — сохранить ссылки на registry, добавить/получить сервисы, базовые подписки
* `Construct` — создать “тяжёлые” штуки, которые не зависят от других нодов
* `BeforeInit/Init/AfterInit` — логика инициализации в 3 фазы (аналог Awake/Start/после прогрева)
* `DisposeInternal` — отписки и чистка (всё что подписывал — отписать)

### Шаблон нода

```csharp
public sealed class MyNode : ConnectorNode
{
    private ServiceRegistry registry;

    public override void Bind(ServiceRegistry registry)
    {
        this.registry = registry;
    }

    public override void Construct(ServiceRegistry registry) { }

    public override void BeforeInit() { }
    public override void Init() { }
    public override void AfterInit() { }

    public override void Tick(float deltaTime) { }
    public override void FixedTick(float fixedDeltaTime) { }
    public override void LateTick(float deltaTime) { }

    protected override void DisposeInternal()
    {
        // отписки
    }
}
```

### Про Unity callbacks

В нодах держи логику в `Bind/Init/Tick...`. (В дебаге у тебя есть валидация, которая ругается на Awake/Start/Update у нодов.)

---

## 4) Порядок выполнения (Order)

### Ноды внутри LocalConnector

* Сортировка: `Order` (IOrder), затем по имени типа
* Где задавать:

  * в инспекторе у нода (`ConnectorNode` уже имеет поле Order)
  * или в `OnValidate` конкретного нода (Editor-only)

### Коннекторы в SceneConnector

* Сортировка: `Order`, затем по имени объекта

---

## 5) LocalConnector: Tick/Pause/Resume/Dispose

### Пауза тиков у коннектора (и рассылка в IPausable ноды)

```csharp
connector.OnPauseRequest(sender: this);   // выключит EnabledTicks и вызовет OnPauseRequest у нодов
connector.OnResumeRequest(sender: this);  // включит обратно и вызовет OnResumeRequest
```

### RunWhenDisabled

* Если нод отключён (`isActiveAndEnabled == false`), он всё равно может тикать, если это `ConnectorNode` и у него включён `RunWhenDisabled`.

### Dispose

* `LocalConnector.Dispose()`:

  * отписывается от сцены
  * вызывает `Dispose()` у нодов с `IDispose`
  * чистит кэши tick интерфейсов

---

## 6) SceneConnectorRegistry: как получить SceneContext извне нодов

```csharp
using UnityEngine.SceneManagement;

var scene = SceneManager.GetActiveScene();

if (SceneConnectorRegistry.TryGet(scene, out var sceneConnector))
{
    var sceneContext = sceneConnector.SceneContext;
    var sceneIndex = sceneContext.Get<SceneEntityIndex>();
}
```

---

## 7) SceneEntityIndex: поиск по id / tag / типу нода

### Как объект попадает в индекс

* `SceneConnector` при Execute регает все статические коннекторы
* Динамические коннекторы регаются через `LocalConnector.OnEnable()` если сцена уже инициализирована
* Для id/tag нужен `EntityKeyBehaviour` на том же объекте, что и `LocalConnector`

### Поиск коннектора по Id

```csharp
var sceneIndex = registry.Get<SceneEntityIndex>();

if (sceneIndex.TryGetById(42, out var connector))
{
    // ок
}

var mustExist = sceneIndex.GetByIdOrThrow(42);
```

### Поиск по Tag

```csharp
if (sceneIndex.TryGetFirstByTag("Chest", out var chest))
{
    // первый попавшийся
}

var all = sceneIndex.GetAllByTag("Chest"); // IReadOnlyList<LocalConnector>
var mustExist = sceneIndex.GetFirstByTagOrThrow("Chest");
```

### Поиск нода по типу (в любой LocalConnector сцены)

```csharp
if (sceneIndex.TryGetFirstNode<MyNode>(out var node, includeDerived: true))
{
    // найден
}

var mustExist = sceneIndex.GetFirstNodeOrThrow<MyNode>(includeDerived: true);
```

### Получить все ноды типа

```csharp
var buffer = new List<MyNode>(64);
var count = sceneIndex.GetNodes(buffer, includeDerived: true);
```

### Найти нод внутри конкретного коннектора

```csharp
if (sceneIndex.TryGetNodeInConnector<MyNode>(connector, out var node))
{
}
```

### Найти нод в первом коннекторе по тегу

```csharp
if (sceneIndex.TryGetNodeInFirstByTag<MyNode>("Chest", out var node))
{
}
```

---

## 8) EntityKeyBehaviour: Id/Tag

* Поля:

  * `Id` (int)
  * `Tag` (string)
  * `AutoAssignId` (bool)
* Editor: кнопка **Assign Unique Id** (для статических id в сценах)
* Runtime: если `Id <= 0` и `AutoAssignId == true`, индекс может назначить id при регистрации

---

## 9) Динамические объекты (spawn/disable/pool)

### Что происходит автоматически

* Если ты заспавнил объект с `LocalConnector`:

  * при `OnEnable()` он попробует зарегаться в `SceneConnector` и выполниться (если сцена уже initialized)
* При `OnDisable()` — разрегистрируется
* `Execute()` у `LocalConnector` вызывается один раз (есть флаг executed)

### Если нужен “полный снос” объекта с корректным Dispose

Используй утилиту:

```csharp
ConnectorDestroyUtils.DisposeAndDestroy(gameObject);
```

---

## 10) Переходы между сценами через EmptySceneTransition + cleanup

В проекте есть `SceneTransitionService`:

* `Go(targetSceneName, doCleanup)`
* грузит `transitionSceneName`
* опционально делает:

  * `Resources.UnloadUnusedAssets()`
  * `GC.Collect()` + `WaitForPendingFinalizers()` + `GC.Collect()`
* потом грузит целевую сцену

### Как зарегистрировать SceneTransitionService один раз (в ProjectContext)

Сделай нод на `ProjectRootConnector` (он DontDestroy) и зарегай сервис в `Bind`:

```csharp
public sealed class SceneTransitionsBootstrapNode : ConnectorNode
{
    [SerializeField] private string transitionSceneName = "EmptySceneTransition";

    public override void Bind(ServiceRegistry registry)
    {
        // runner = этот нод (MonoBehaviour), он живёт вместе с ProjectRootConnector
        ProjectServices.Add(new SceneTransitionService(runner: this, transitionSceneName: transitionSceneName));
    }
}
```

### Как вызывать переход из любого места

```csharp
ProjectServices.Get<SceneTransitionService>().Go("Level_2", doCleanup: true);
```

### Примечание про SceneConnector в transition-сцене

* В `EmptySceneTransition` можно не иметь `SceneConnector`.
* В Editor у тебя может быть warn, если `SceneConnector` не найден — либо игнорируй, либо добавь пустой `SceneConnector` в transition-сцену.

---

## 11) AppLifecycleService: фокус/пауза/выход

`SceneOrchestrator` гарантирует наличие `AppLifecycleService` в `ProjectContext`.

### Подписка из нода

```csharp
public sealed class LifecycleListenerNode : ConnectorNode
{
    private AppLifecycleService lifecycle;

    public override void Bind(ServiceRegistry registry)
    {
        lifecycle = ProjectServices.Get<AppLifecycleService>();
        lifecycle.FocusChanged += OnFocusChanged;
        lifecycle.PauseChanged += OnPauseChanged;
        lifecycle.Quit += OnQuit;
    }

    protected override void DisposeInternal()
    {
        if (lifecycle == null)
            return;

        lifecycle.FocusChanged -= OnFocusChanged;
        lifecycle.PauseChanged -= OnPauseChanged;
        lifecycle.Quit -= OnQuit;
    }

    private void OnFocusChanged(bool hasFocus, Object sender) { }
    private void OnPauseChanged(bool paused, Object sender) { }
    private void OnQuit(Object sender) { }
}
```

---

## 12) Быстрые рецепты (1 строка)

* Получить ProjectContext:

  * ```var project = ProjectServices.Context;```
* Получить SceneContext из MonoBehaviour:

  * ```SceneConnectorRegistry.TryGet(gameObject.scene, out var sc); var ctx = sc.SceneContext;```
* Получить SceneEntityIndex из SceneContext:

  * ``var index = ctx.Get<SceneEntityIndex>();``
* Найти объект по тегу:

  * ``var door = index.GetFirstByTagOrThrow("Door");``
* Найти нод по типу:

  * ``var ui = index.GetFirstNodeOrThrow<MyUiNode>();``
* Усыпить тики у конкретного LocalConnector:

  * ``connector.OnPauseRequest(sender: this);``
* Удалить объект “правильно”:

  * ``ConnectorDestroyUtils.DisposeAndDestroy(go);``

---

