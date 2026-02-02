using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Управляет режимом визуализации грида (Cell / Area)
/// Диспетчер между GridHighlightSystem и конкретными визуализаторами
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GridHighlightSystem))]
public partial class GridVisualizationManager : SystemBase
{
    private GridVisualMode _lastMode = GridVisualMode.Cell;
    private InteractionMode _lastInteractionMode = InteractionMode.None;

    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<ActiveUnitComponent>();
    }

    protected override void OnUpdate()
    {
        var config = SystemAPI.GetSingleton<GridConfig>();
        var selection = SystemAPI.GetSingleton<ActiveUnitComponent>();
        var currentMode = config.VisualMode;
        var interactionMode = selection.Mode;
        var selectedUnit = selection.Unit;

        // Проверяем смену режима визуализации
        bool modeChanged = currentMode != _lastMode;

        if (modeChanged)
        {
            CleanupMode(_lastMode);
            _lastMode = currentMode;
        }

        _lastInteractionMode = interactionMode;

        // Активируем нужный режим
        if (currentMode == GridVisualMode.Area)
        {
            // Если юнит не выбран или нет режима — удаляем overlay
            if (selectedUnit == Entity.Null || interactionMode == InteractionMode.None)
            {
                CleanupAreaOverlay();
            }
            else
            {
                RequestAreaVisualization(interactionMode);
            }
        }
        // Cell режим не требует действий (GridHighlightSystem уже изменила цвета)
    }

    private void RequestAreaVisualization(InteractionMode mode)
    {
        // Создаем или обновляем запрос
        Entity requestEntity;

        if (SystemAPI.TryGetSingletonEntity<AreaOverlayRequest>(out requestEntity))
        {
            // Обновляем существующий запрос
            EntityManager.SetComponentData(requestEntity, new AreaOverlayRequest { Mode = mode });
        }
        else
        {
            // Создаем новый запрос
            requestEntity = EntityManager.CreateEntity();
            EntityManager.AddComponentData(requestEntity, new AreaOverlayRequest { Mode = mode });
            EntityManager.AddBuffer<OverlayCell>(requestEntity);
        }

        // Заполняем буфер highlighted клетками
        var buffer = EntityManager.GetBuffer<OverlayCell>(requestEntity);
        buffer.Clear();

        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        var mapBuffer = EntityManager.GetBuffer<GridCellElement>(mapEntity);
        var gridSize = SystemAPI.GetSingleton<GridConfig>().GridSize;

        for (int i = 0; i < mapBuffer.Length; i++)
        {
            if (mapBuffer[i].IsHighlighted)
            {
                int x = i / gridSize.y;
                int y = i % gridSize.y;
                buffer.Add(new OverlayCell { GridPos = new int2(x, y) });
            }
        }
    }

    private void CleanupMode(GridVisualMode mode)
    {
        if (mode == GridVisualMode.Area)
        {
            CleanupAreaOverlay();
        }
    }

    private void CleanupAreaOverlay()
    {
        // Удаляем активный overlay
        if (SystemAPI.TryGetSingletonEntity<ActiveOverlayData>(out var dataEntity))
        {
            var data = EntityManager.GetComponentData<ActiveOverlayData>(dataEntity);

            // Mesh
            if (data.MeshEntity != Entity.Null && EntityManager.Exists(data.MeshEntity))
            {
                if (EntityManager.HasComponent<MeshRendererReference>(data.MeshEntity))
                {
                    var meshRef = EntityManager.GetComponentObject<MeshRendererReference>(data.MeshEntity);
                    if (meshRef != null && meshRef.GameObject != null)
                        Object.Destroy(meshRef.GameObject);
                }

                EntityManager.DestroyEntity(data.MeshEntity);
            }

            // 🔥 ДОБАВЛЕНО: граница (LineRenderer / Decal)
            if (data.DecalEntity != Entity.Null && EntityManager.Exists(data.DecalEntity))
            {
                if (EntityManager.HasComponent<LineRendererReference>(data.DecalEntity))
                {
                    var lineRef = EntityManager.GetComponentObject<LineRendererReference>(data.DecalEntity);
                    if (lineRef != null && lineRef.GameObject != null)
                        Object.Destroy(lineRef.GameObject);
                }

                EntityManager.DestroyEntity(data.DecalEntity);
            }

            // Сам синглтон
            EntityManager.DestroyEntity(dataEntity);
        }

        // Удаляем запросы
        if (SystemAPI.TryGetSingletonEntity<AreaOverlayRequest>(out var reqEntity))
            EntityManager.DestroyEntity(reqEntity);
    }

}
