using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(ZoneSpawnerSystem))]
public partial struct ZoneEventGeneratorSystem : ISystem
{
    private bool _hasGenerated;

    public void OnCreate(ref SystemState state)
    {
        _hasGenerated = false;
        state.RequireForUpdate<ZoneModeTag>();
        state.RequireForUpdate<ZoneEventConfig>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_hasGenerated) return;
        if (!SystemAPI.HasSingleton<GridMapTag>()) return;

        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        if (!state.EntityManager.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var eventConfig = SystemAPI.GetSingleton<ZoneEventConfig>();
        var gridConfig = SystemAPI.GetSingleton<GridConfig>();
        var radiationBuffer = state.EntityManager.GetBuffer<ZoneCellRadiation>(mapEntity);

        // ── Шаг 1: собираем всё в managed лист, не трогая entities ──
        var pending = new System.Collections.Generic.List<(ZoneEventType type, int visibility, int2 pos, float3 worldPos)>();
        var random = Unity.Mathematics.Random.CreateFromIndex(9999);

        for (int i = 0; i < radiationBuffer.Length; i++)
        {
            var cell = radiationBuffer[i];
            if (cell.RadiationLevel >= 15) continue;

            float roll = random.NextFloat();
            ZoneEventType eventType = ZoneEventType.None;

            if (roll < eventConfig.AnomalyProbability) eventType = ZoneEventType.Anomaly;
            else if (roll < eventConfig.AnomalyProbability + eventConfig.FightProbability) eventType = ZoneEventType.Fight;
            else if (roll < eventConfig.AnomalyProbability + eventConfig.FightProbability + eventConfig.EventProbability) eventType = ZoneEventType.Event;

            if (eventType == ZoneEventType.None) continue;

            int visibility = random.NextInt(0, 4);
            float3 worldPos = HexGridUtils.HexAxialToWorld(cell.GridPos, gridConfig.Spacing);
            worldPos.y = 1.0f;

            pending.Add((eventType, visibility, cell.GridPos, worldPos));
        }

        // ── Шаг 2: создаём entity и буфер уже после прохода по буферу ──
        if (!state.EntityManager.HasBuffer<ZoneEventElement>(mapEntity))
            state.EntityManager.AddBuffer<ZoneEventElement>(mapEntity);

        // После AddBuffer буфер тоже инвалиден — берём заново
        var eventBuffer = state.EntityManager.GetBuffer<ZoneEventElement>(mapEntity);
        eventBuffer.Clear();

        foreach (var (type, visibility, pos, worldPos) in pending)
        {
            var eventEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(eventEntity, $"ZoneEvent_{type}_{pos.x}_{pos.y}");

            state.EntityManager.AddComponentData(eventEntity, new ZoneEventData
            {
                EventType = type,
                Visibility = visibility,
                IsDiscovered = false,
                GridPos = pos
            });
            state.EntityManager.AddComponentData(eventEntity, LocalTransform.FromPosition(worldPos));

            // После CreateEntity буфер снова инвалиден — берём заново каждый раз
            eventBuffer = state.EntityManager.GetBuffer<ZoneEventElement>(mapEntity);
            eventBuffer.Add(new ZoneEventElement
            {
                EventEntity = eventEntity,
                GridPos = pos,
                EventType = type,
                Visibility = visibility,
                IsDiscovered = false
            });
        }

        _hasGenerated = true;
        Debug.Log($"[ZoneEventGenerator] Generated {eventBuffer.Length} events on the map");
    }
}