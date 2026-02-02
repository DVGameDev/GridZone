using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Система обнаружения событий на карте в радиусе видимости героя
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitMoveSystem))]
public partial class ZoneEventDiscoverySystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ZoneModeTag>();
        RequireForUpdate<GridMapTag>();
    }

    protected override void OnUpdate()
    {
        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        if (!EntityManager.HasBuffer<ZoneEventElement>(mapEntity)) return;

        var eventBuffer = EntityManager.GetBuffer<ZoneEventElement>(mapEntity);
        if (eventBuffer.Length == 0) return;

        var gridSize = SystemAPI.GetSingleton<GridConfig>().GridSize;

        // Находим героя (UnitId = 0)
        foreach (var (gridPos, unitId) in SystemAPI.Query<RefRO<GridCoordinates>, RefRO<UnitIdComponent>>())
        {
            if (unitId.ValueRO.UnitId != 0) continue;

            int2 heroPos = gridPos.ValueRO.Value;

            // Проверяем все события
            for (int i = 0; i < eventBuffer.Length; i++)
            {
                var eventElement = eventBuffer[i];
                
                // Уже обнаружено - пропускаем
                if (eventElement.IsDiscovered) continue;

                // Вычисляем расстояние до героя
                int distance = HexGridUtils.HexDistance(heroPos, eventElement.GridPos);

                // Проверяем, видно ли событие на этом расстоянии
                if (distance <= eventElement.Visibility)
                {
                    // Обнаружено!
                    eventElement.IsDiscovered = true;
                    eventBuffer[i] = eventElement;

                    // Обновляем entity события
                    if (EntityManager.Exists(eventElement.EventEntity))
                    {
                        var eventData = EntityManager.GetComponentData<ZoneEventData>(eventElement.EventEntity);
                        eventData.IsDiscovered = true;
                        EntityManager.SetComponentData(eventElement.EventEntity, eventData);
                    }

                    Debug.Log($"[ZoneEventDiscovery] Discovered {eventElement.EventType} at {eventElement.GridPos}, visibility={eventElement.Visibility}");
                }
            }

            break; // Только один герой
        }
    }
}
