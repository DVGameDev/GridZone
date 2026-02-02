using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;
using UnityEngine;

/// <summary>
/// Система генерации ZONE карты (Hex Grid + радиация)
/// Запускается ВМЕСТО GridSpawnerSystem/HexGridSpawnerSystem
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct ZoneSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 🔥 Проверяем, что GridConfig еще не создан
        var configQuery = SystemAPI.QueryBuilder().WithAll<GridConfig>().Build();
        if (!configQuery.IsEmpty) return;

        // 🔥 Ищем ZoneSpawnerComponent
        var query = SystemAPI.QueryBuilder().WithAll<ZoneSpawnerComponent>().Build();
        if (query.IsEmpty) return;

        var spawnerEntities = query.ToEntityArray(Allocator.Temp);
        var spawnerComponents = query.ToComponentDataArray<ZoneSpawnerComponent>(Allocator.Temp);

        if (spawnerEntities.Length > 0)
        {
            var spawnerEntity = spawnerEntities[0];
            var spawnerData = spawnerComponents[0];
            var radiationConfig = state.EntityManager.GetComponentData<ZoneRadiationConfig>(spawnerEntity);
            var islandConfig = state.EntityManager.GetComponentData<ZoneIslandConfig>(spawnerEntity);

            int qCount = spawnerData.GridSize.x;
            int rCount = spawnerData.GridSize.y;
            int totalCells = qCount * rCount;

            Debug.Log($"[ZoneSpawnerSystem] Generating ZONE map {qCount}x{rCount}...");

            // 1. Создаем GridMap (совместимо с основным кодом)
            var mapEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(mapEntity, "GridMap");
            state.EntityManager.AddComponentData(mapEntity, new GridMapTag { Size = spawnerData.GridSize });
            state.EntityManager.AddBuffer<GridCellElement>(mapEntity);

            // 🔥 Добавляем буфер радиации (отдельный)
            state.EntityManager.AddBuffer<ZoneCellRadiation>(mapEntity);

            // 2. Создаем GridConfig (для совместимости с системами движения)
            var configEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(configEntity, "GridConfig");
            state.EntityManager.AddComponentData(configEntity, new GridConfig
            {
                GridSize = spawnerData.GridSize,
                BrushSize = new int2(1, 1),
                Spacing = spawnerData.HexSize,
                HeightSky = 3.0f,
                HeightGround = 0.0f,
                HeightUnderground = -3.0f,
                FacingMode = UnitFacingMode.Free,
                VisualMode = GridVisualMode.Cell, // Cell mode для ZONE
                Layout = GridLayoutType.HexFlatTop
            });

            // 3. Инстанцируем hex клетки
            var instances = new NativeArray<Entity>(totalCells, Allocator.TempJob);
            state.EntityManager.Instantiate(spawnerData.HexCellPrefab, instances);
            state.EntityManager.AddComponent<GridCoordinates>(instances);
            state.EntityManager.AddComponent<URPMaterialPropertyBaseColor>(instances);
            state.EntityManager.AddComponent<CellCustomColor>(instances); // 🔥 Для кастомных цветов

            // 4. Получаем буферы
            var mapBuffer = state.EntityManager.GetBuffer<GridCellElement>(mapEntity);
            var radiationBuffer = state.EntityManager.GetBuffer<ZoneCellRadiation>(mapEntity);
            mapBuffer.ResizeUninitialized(totalCells);
            radiationBuffer.ResizeUninitialized(totalCells);

            // 5. Инициализация через Job
            var initJob = new InitializeZoneGridJob
            {
                Instances = instances,
                GridSize = new int2(qCount, rCount),
                HexSize = spawnerData.HexSize,
                YellowRadiation = radiationConfig.LevelYellow,
                YellowColor = radiationConfig.ColorYellow,
                Transforms = state.GetComponentLookup<LocalTransform>(false),
                Coordinates = state.GetComponentLookup<GridCoordinates>(false),
                Colors = state.GetComponentLookup<URPMaterialPropertyBaseColor>(false),
                CustomColors = state.GetComponentLookup<CellCustomColor>(false),
                MapBuffer = mapBuffer,
                RadiationBuffer = radiationBuffer
            };

            var jobHandle = initJob.Schedule(totalCells, 64);
            jobHandle.Complete();

            instances.Dispose();

            // 🔥 Удаляем ZoneSpawnerComponent, оставляем конфиги как синглтоны
            state.EntityManager.RemoveComponent<ZoneSpawnerComponent>(spawnerEntity);
            state.EntityManager.SetName(spawnerEntity, "ZoneConfig");

            Debug.Log("[ZoneSpawnerSystem] ZONE map generated successfully!");
        }

        spawnerEntities.Dispose();
        spawnerComponents.Dispose();
    }

    /// <summary>
    /// Burst Job для инициализации ZONE карты (все клетки Yellow)
    /// </summary>
    [BurstCompile]
    private struct InitializeZoneGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Entity> Instances;
        [ReadOnly] public int2 GridSize;
        [ReadOnly] public float HexSize;
        [ReadOnly] public int YellowRadiation;
        [ReadOnly] public float4 YellowColor;

        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> Transforms;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridCoordinates> Coordinates;
        [NativeDisableParallelForRestriction] public ComponentLookup<URPMaterialPropertyBaseColor> Colors;
        [NativeDisableParallelForRestriction] public ComponentLookup<CellCustomColor> CustomColors;
        [NativeDisableParallelForRestriction] public DynamicBuffer<GridCellElement> MapBuffer;
        [NativeDisableParallelForRestriction] public DynamicBuffer<ZoneCellRadiation> RadiationBuffer;

        public void Execute(int index)
        {
            // Axial координаты
            int q = index / GridSize.y;
            int r = index % GridSize.y;

            var instance = Instances[index];

            // Позиция
            float3 pos = HexGridUtils.HexAxialToWorld(new int2(q, r), HexSize);
            Transforms[instance] = LocalTransform.FromPositionRotation(pos, quaternion.identity);

            // Координаты
            Coordinates[instance] = new GridCoordinates { Value = new int2(q, r) };

            // 🔥 Цвет радиации (Yellow с прозрачностью)
            Colors[instance] = new URPMaterialPropertyBaseColor { Value = YellowColor };
            CustomColors[instance] = new CellCustomColor { BaseColor = YellowColor };

            // Заполняем GridCellElement
            MapBuffer[index] = new GridCellElement
            {
                CellEntity = instance,
                IsOccupiedGround = false,
                IsOccupiedUnderground = false,
                IsOccupiedSky = false,
                OccupantGround = Entity.Null,
                OccupantUnderground = Entity.Null,
                OccupantSky = Entity.Null,
                IsHighlighted = false
            };

            // 🔥 Заполняем радиацию (все клетки Yellow, не посещены)
            RadiationBuffer[index] = new ZoneCellRadiation
            {
                GridPos = new int2(q, r),
                RadiationLevel = YellowRadiation,
                IsVisited = false
            };
        }
    }
}
