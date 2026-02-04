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
            var baseGridColor = state.EntityManager.GetComponentData<ZoneBaseGridColor>(spawnerEntity);
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
            
            // 🔥 Сохраняем базовый цвет грида на GridMap для системы дебага радиации
            state.EntityManager.AddComponentData(mapEntity, baseGridColor);

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

            // 5. Инициализация через Job (базовая Yellow карта)
            var initJob = new InitializeZoneGridJob
            {
                Instances = instances,
                GridSize = new int2(qCount, rCount),
                HexSize = spawnerData.HexSize,
                YellowRadiation = radiationConfig.LevelYellow,
                BaseGrayColor = baseGridColor.Color, // 🔥 ИЗМЕНЕНО: базовый серый цвет
                Transforms = state.GetComponentLookup<LocalTransform>(false),
                Coordinates = state.GetComponentLookup<GridCoordinates>(false),
                Colors = state.GetComponentLookup<URPMaterialPropertyBaseColor>(false),
                CustomColors = state.GetComponentLookup<CellCustomColor>(false),
                MapBuffer = mapBuffer,
                RadiationBuffer = radiationBuffer
            };

            var jobHandle = initJob.Schedule(totalCells, 64);
            jobHandle.Complete();

            // 🔥 6. Генерация островов радиации
            // ✅ Полностью Burst-совместимо
            double time = SystemAPI.Time.ElapsedTime;
            uint frameCount = (uint)UnityEngine.Time.frameCount; // Это можно вызывать вне Burst

            // Создаём уникальный seed из времени и счётчика кадров
            uint seed = math.hash(new uint2(
                (uint)(time * 1000000.0),
                frameCount
            ));

           // var random = Unity.Mathematics.Random.CreateFromIndex(seed);
            ZoneIslandGenerator.GenerateGreenIslands(radiationBuffer, spawnerData.GridSize, radiationConfig, islandConfig, seed);
            ZoneIslandGenerator.GenerateOrangeIslands(radiationBuffer, spawnerData.GridSize, radiationConfig, islandConfig, (seed + 1000));
            ZoneIslandGenerator.GenerateRedIslands(radiationBuffer, spawnerData.GridSize, radiationConfig, islandConfig, (seed + 2000));

            // 🔥 7. Применение цветов к клеткам
            //ApplyRadiationColorsToCells(radiationBuffer, radiationConfig, colorsLookup: state.GetComponentLookup<URPMaterialPropertyBaseColor>(false), customColorsLookup: state.GetComponentLookup<CellCustomColor>(false));

            instances.Dispose();

        }
    }

    /// <summary>
    /// Применяет цвета радиации ко всем клеткам
    /// </summary>
    private static void ApplyRadiationColorsToCells(
        DynamicBuffer<ZoneCellRadiation> radiationBuffer,
        ZoneRadiationConfig radiationConfig,
        ComponentLookup<URPMaterialPropertyBaseColor> colorsLookup,
        ComponentLookup<CellCustomColor> customColorsLookup)
    {
        for (int i = 0; i < radiationBuffer.Length; i++)
        {
            var radiation = radiationBuffer[i];

            float4 cellColor;
            switch (radiation.RadiationLevel)
            {
                case 0: cellColor = radiationConfig.ColorGreen; break;
                case 5: cellColor = radiationConfig.ColorYellow; break;
                case 10: cellColor = radiationConfig.ColorOrange; break;
                case 15: cellColor = radiationConfig.ColorRed; break;
                default: cellColor = radiationConfig.ColorYellow; break;
            }

            var cellEntity = radiationBuffer[i].CellEntity; // 🔥 Нужно добавить CellEntity в ZoneCellRadiation!
            if (colorsLookup.HasComponent(cellEntity))
                colorsLookup[cellEntity] = new URPMaterialPropertyBaseColor { Value = cellColor };
            if (customColorsLookup.HasComponent(cellEntity))
                customColorsLookup[cellEntity] = new CellCustomColor { BaseColor = cellColor };
        }
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
        [ReadOnly] public float4 BaseGrayColor;


        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> Transforms;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridCoordinates> Coordinates;
        [NativeDisableParallelForRestriction] public ComponentLookup<URPMaterialPropertyBaseColor> Colors;
        [NativeDisableParallelForRestriction] public ComponentLookup<CellCustomColor> CustomColors;
        [NativeDisableParallelForRestriction] public DynamicBuffer<GridCellElement> MapBuffer;
        [NativeDisableParallelForRestriction] public DynamicBuffer<ZoneCellRadiation> RadiationBuffer;

        public void Execute(int index)
        {
            int x = index % GridSize.x;   // колонка (X → право)
    int z = index / GridSize.x;   // строка (Z → вверх)

    // odd-q offset → axial (FlatTop)
    int q = x;
            int r = z - (x >> 1);

            var instance = Instances[index];

            float3 pos = HexGridUtils.HexAxialToWorld(new int2(q, r), HexSize);
            Transforms[instance] = LocalTransform.FromPositionRotation(pos, quaternion.identity);

            Coordinates[instance] = new GridCoordinates
            {
                Value = new int2(q, r)
            };

            Colors[instance] = new URPMaterialPropertyBaseColor { Value = BaseGrayColor };
            CustomColors[instance] = new CellCustomColor { BaseColor = BaseGrayColor };

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

            RadiationBuffer[index] = new ZoneCellRadiation
            {
                GridPos = new int2(q, r),
                CellEntity = instance,
                RadiationLevel = YellowRadiation,
                IsVisited = false
            };
        }

    }
}
