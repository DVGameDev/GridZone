using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Система генерации событий на карте (аномалии, драки, ивенты)
/// Запускается после генерации карты
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(ZoneSpawnerSystem))]
[BurstCompile] // Включаем Burst для всей системы
public partial struct ZoneEventGeneratorSystem : ISystem
{
    private bool _hasGenerated;

    public void OnCreate(ref SystemState state)
    {
        _hasGenerated = false;
        state.RequireForUpdate<ZoneModeTag>();
        state.RequireForUpdate<ZoneEventConfig>();
        state.RequireForUpdate<GridMapTag>();
        state.RequireForUpdate<GridConfig>();

        // Используем EndInitialization, чтобы события создались и стали доступны 
        // до начала SimulationSystemGroup в этом же кадре
        state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (_hasGenerated) return;

        // Проверяем наличие карты
        if (!SystemAPI.HasSingleton<GridMapTag>()) return;
        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();

        // ЭТАП 1: Структурные изменения (Critical Section)
        // Сначала добавляем буфер, если его нет. Это меняет архетип сущности.
        // Если сделать это ПОСЛЕ получения radiationBuffer, то handle буфера протухнет.
        if (!state.EntityManager.HasBuffer<ZoneEventElement>(mapEntity))
        {
            state.EntityManager.AddBuffer<ZoneEventElement>(mapEntity);
        }

        // ЭТАП 2: Получение данных (Data Access)
        // Теперь безопасно получаем ссылки на буферы
        if (!state.EntityManager.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var radiationBuffer = state.EntityManager.GetBuffer<ZoneCellRadiation>(mapEntity);
        var eventBuffer = state.EntityManager.GetBuffer<ZoneEventElement>(mapEntity);

        // Очищаем буфер событий (это не структурное изменение, просто изменение данных)
        eventBuffer.Clear();

        var eventConfig = SystemAPI.GetSingleton<ZoneEventConfig>();
        var gridConfig = SystemAPI.GetSingleton<GridConfig>();

        // ЭТАП 3: Создание команд (Command Recording)
        var ecbSingleton = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>();
        var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

        // Запускаем генерацию
        GenerateEvents(ref state, eventConfig, gridConfig, radiationBuffer, mapEntity, ref ecb);

        _hasGenerated = true;
        // Debug.Log внутри Burst не работает напрямую с managed строками, поэтому лучше убрать или использовать conditional
    }

    [BurstCompile]
    private void GenerateEvents(
        ref SystemState state,
        ZoneEventConfig config,
        GridConfig gridConfig,
        DynamicBuffer<ZoneCellRadiation> radiationBuffer,
        Entity mapEntity,
        ref EntityCommandBuffer ecb)
    {
        // Используем детерминированный Random для воспроизводимости (или Time.ElapsedTime для рандома)
        var random = Unity.Mathematics.Random.CreateFromIndex(9999);
        float3 gridSpacing = gridConfig.Spacing;

        for (int i = 0; i < radiationBuffer.Length; i++)
        {
            var cell = radiationBuffer[i];

            // Пропускаем красные зоны
            if (cell.RadiationLevel >= 15) continue;

            ZoneEventType eventType = ZoneEventType.None;
            float roll = random.NextFloat();

            // Логика вероятностей
            if (roll < config.AnomalyProbability)
            {
                eventType = ZoneEventType.Anomaly;
            }
            else if (roll < config.AnomalyProbability + config.FightProbability)
            {
                eventType = ZoneEventType.Fight;
            }
            else if (roll < config.AnomalyProbability + config.FightProbability + config.EventProbability)
            {
                eventType = ZoneEventType.Event;
            }

            if (eventType == ZoneEventType.None) continue;

            int visibility = random.NextInt(0, 4);

            // 1. Создаем сущность через ECB
            var eventEntity = ecb.CreateEntity();

            // 2. Имя (используем FixedString для Burst-совместимости)
            // Обычные строки ($"ZoneEvent...") вызовут ошибку в Burst
            ecb.SetName(eventEntity, new FixedString64Bytes("ZoneEvent"));

            ecb.AddComponent(eventEntity, new ZoneEventData
            {
                EventType = eventType,
                Visibility = visibility,
                IsDiscovered = false,
                GridPos = cell.GridPos
            });

            // 3. Позиция
            float3 worldPos = HexGridUtils.HexAxialToWorld(cell.GridPos, gridSpacing.x);
            worldPos.y = 1.0f; // Немного выше грида

            ecb.AddComponent(eventEntity, LocalTransform.FromPosition(worldPos));

            // 4. Добавляем запись в буфер на карте через ECB
            ecb.AppendToBuffer(mapEntity, new ZoneEventElement
            {
                EventEntity = eventEntity,
                GridPos = cell.GridPos,
                EventType = eventType,
                Visibility = visibility,
                IsDiscovered = false
            });
        }
    }
}
