# Zenject-Like Installers In AMNF (RU)

- Актуально для ветки пакета: `v3.x`

Этот подход нужен тем, кто привык к installer-стилю Zenject.

## 1) Project Installer Node

Назначение:
- регистрировать глобальные сервисы/конфиги в `ProjectContext`.

Где вешать:
- на `ProjectRootConnector` prefab (или его дочерний объект под `ProjectRootConnector`).

Когда выполняется:
- один раз при старте приложения (когда создается `ProjectRootConnector`).

## 2) Scene Installer Node

Назначение:
- регистрировать сервисы только для текущей сцены (`SceneContext`).

Где вешать:
- на `LocalConnector`, который должен запускаться раньше остальных сценовых фич.

Рекомендация:
- выставить installer-ноду с `Order = -1000`.

## 3) Мини-паттерн

```csharp
public override void Bind(ServiceContainer registry)
{
    // Register
}

public override void Construct(ServiceContainer registry)
{
    // Resolve
}

protected override void DisposeInternal()
{
    // Unsubscribe / remove
}
```

## 4) Что уже есть в пакете

Смотри готовые примеры:
- `Example/Installers/ProjectInstallerNode.cs`
- `Example/Installers/SceneInstallerNode.cs`
- `Example/CompositionRoot/CompositionRootNode.cs`
