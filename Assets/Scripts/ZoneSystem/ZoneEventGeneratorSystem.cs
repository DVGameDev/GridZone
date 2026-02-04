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

        // ── Шаг 1: КОПИРУЕМ данные из буфера, чтобы он не инвалидировался ──
        var cellDataList = new System.Collections.Generic.List<(int2 gridPos, int radiationLevel)>();
        for (int i = 0; i < radiationBuffer.Length; i++)
        {
            var cell = radiationBuffer[i];
            cellDataList.Add((cell.GridPos, cell.RadiationLevel));
        }

        // ── Шаг 2: генерируем события на основе скопированных данных ──
        var pending = new System.Collections.Generic.List<(Entity entity, int2 pos, ZoneEventType type, int visibility)>();
        uint seed = (uint)(System.DateTime.Now.Ticks ^ UnityEngine.Random.Range(1, 999999));
        var random = Unity.Mathematics.Random.CreateFromIndex(seed);


        foreach (var (gridPos, radiationLevel) in cellDataList)
        {
            if (radiationLevel >= 15) continue;

            float roll = random.NextFloat();
            ZoneEventType eventType = ZoneEventType.None;

            if (roll < eventConfig.AnomalyProbability)
                eventType = ZoneEventType.Anomaly;
            else if (roll < eventConfig.AnomalyProbability + eventConfig.FightProbability)
                eventType = ZoneEventType.Fight;
            else if (roll < eventConfig.AnomalyProbability + eventConfig.FightProbability + eventConfig.EventProbability)
                eventType = ZoneEventType.Event;

            if (eventType == ZoneEventType.None) continue;

            int visibility = random.NextInt(0, 4);
            float3 worldPos = HexGridUtils.HexAxialToWorld(gridPos, gridConfig.Spacing);
            worldPos.y = 1.0f;

            // 🔥 Создаем entity (это инвалидирует буферы, но нам уже не важно)
            var eventEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(eventEntity, $"ZoneEvent_{eventType}_{gridPos.x}_{gridPos.y}");
            state.EntityManager.AddComponentData(eventEntity, new ZoneEventData
            {
                EventType = eventType,
                Visibility = visibility,
                IsDiscovered = false,
                GridPos = gridPos
            });
            state.EntityManager.AddComponentData(eventEntity, LocalTransform.FromPosition(worldPos));

            pending.Add((eventEntity, gridPos, eventType, visibility));
        }

        // ── Шаг 3: Один раз получаем/создаем буфер и заполняем его ──
        if (!state.EntityManager.HasBuffer<ZoneEventElement>(mapEntity))
            state.EntityManager.AddBuffer<ZoneEventElement>(mapEntity);

        var eventBuffer = state.EntityManager.GetBuffer<ZoneEventElement>(mapEntity);
        eventBuffer.Clear();

        foreach (var (entity, pos, type, visibility) in pending)
        {
            eventBuffer.Add(new ZoneEventElement
            {
                EventEntity = entity,
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
