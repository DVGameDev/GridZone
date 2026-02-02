using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;

/// <summary>
/// Система генерации Hex Grid (Flat-Top, Axial координаты)
/// Полностью совместима с Quad Grid системой
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct HexGridSpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 🔥 Проверяем, что GridConfig еще не создан
        var configQuery = SystemAPI.QueryBuilder().WithAll<GridConfig>().Build();
        if (!configQuery.IsEmpty) return;

        // 🔥 Ищем GridSpawnerComponent с Layout == HexFlatTop
        var query = SystemAPI.QueryBuilder().WithAll<GridSpawnerComponent>().Build();
        if (query.IsEmpty) return;

        var spawnerEntities = query.ToEntityArray(Allocator.Temp);
        var spawnerComponents = query.ToComponentDataArray<GridSpawnerComponent>(Allocator.Temp);

        if (spawnerEntities.Length > 0)
        {
            var spawnerEntity = spawnerEntities[0];
            var spawnerData = spawnerComponents[0];

            // 🔥 КЛЮЧЕВАЯ ПРОВЕРКА: работаем только с Hex
            if (spawnerData.Layout != GridLayoutType.HexFlatTop)
            {
                spawnerEntities.Dispose();
                spawnerComponents.Dispose();
                return;
            }

            int qCount = spawnerData.GridSize.x;
            int rCount = spawnerData.GridSize.y;
            int totalCells = qCount * rCount;

            // 1. Создаем GridMap (единый с Quad)
            var mapEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(mapEntity, "GridMap");
            state.EntityManager.AddComponentData(mapEntity, new GridMapTag { Size = spawnerData.GridSize });
            state.EntityManager.AddBuffer<GridCellElement>(mapEntity);

            // 2. Создаем GridConfig (единый с Quad)
            var configEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(configEntity, "GridConfig");
            state.EntityManager.AddComponentData(configEntity, new GridConfig
            {
                GridSize = spawnerData.GridSize,
                BrushSize = spawnerData.InitialBrushSize,
                Spacing = spawnerData.Spacing,
                HeightSky = spawnerData.HeightSky,
                HeightGround = spawnerData.HeightGround,
                HeightUnderground = spawnerData.HeightUnderground,
                FacingMode = spawnerData.FacingMode,
                VisualMode = spawnerData.VisualMode,
                Layout = GridLayoutType.HexFlatTop // 🔥 ВАЖНО
            });

            // 3. Инстанцируем hex префабы
            var instances = new NativeArray<Entity>(totalCells, Allocator.TempJob);
            state.EntityManager.Instantiate(spawnerData.PrefabEntity, instances);
            state.EntityManager.AddComponent<GridCoordinates>(instances);
            state.EntityManager.AddComponent<URPMaterialPropertyBaseColor>(instances);

            // 4. Получаем буфер карты
            var mapBuffer = state.EntityManager.GetBuffer<GridCellElement>(mapEntity);
            mapBuffer.ResizeUninitialized(totalCells);

            // 🔥 Параллельная инициализация через Job
            // 🔥 Читаем GridColorConfig ДО создания Job
            var colors = SystemAPI.GetSingleton<GridColorConfig>();

            var initJob = new InitializeHexGridJob
            {
                Instances = instances,
                GridSize = new int2(qCount, rCount),
                HexSize = spawnerData.Spacing,
                RandomSeed = 1234,
                Transforms = state.GetComponentLookup<LocalTransform>(false),
                Coordinates = state.GetComponentLookup<GridCoordinates>(false),
                Colors = state.GetComponentLookup<URPMaterialPropertyBaseColor>(false),
                MapBuffer = mapBuffer,
                ColorGray = colors.ColorGray // 🔥 Передаем цвет в Job
            };


            var jobHandle = initJob.Schedule(totalCells, 64);
            jobHandle.Complete();

            instances.Dispose();

            // 🔥 Удаляем GridSpawnerComponent, оставляем GridColorConfig как синглтон
            state.EntityManager.RemoveComponent<GridSpawnerComponent>(spawnerEntity);
            state.EntityManager.SetName(spawnerEntity, "GridColorConfig");
        }

        spawnerEntities.Dispose();
        spawnerComponents.Dispose();
    }

    /// <summary>
    /// Burst-компилируемая Job для параллельной инициализации Hex Grid
    /// </summary>
    [BurstCompile]
    private struct InitializeHexGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Entity> Instances;
        [ReadOnly] public int2 GridSize;
        [ReadOnly] public float HexSize;
        [ReadOnly] public uint RandomSeed;

        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> Transforms;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridCoordinates> Coordinates;
        [NativeDisableParallelForRestriction] public ComponentLookup<URPMaterialPropertyBaseColor> Colors;
        [NativeDisableParallelForRestriction] public DynamicBuffer<GridCellElement> MapBuffer;
        [ReadOnly] public float4 ColorGray; // 🔥 Цвет из конфига

        public void Execute(int index)
        {
            // Вычисляем Axial координаты (q, r) для прямоугольной области
            int q = index % GridSize.x;
            int r = index / GridSize.x;

            var instance = Instances[index];

            // Позиция через HexGridUtils
            float3 pos = HexGridUtils.HexAxialToWorld(new int2(q, r), HexSize);
            Transforms[instance] = LocalTransform.FromPositionRotation(pos, quaternion.identity);

            // Координаты (Axial)
            Coordinates[instance] = new GridCoordinates { Value = new int2(q, r) };

            // Генерация препятствий (детерминированный рандом)
            var random = Unity.Mathematics.Random.CreateFromIndex((uint)index + RandomSeed);
            bool isWall = random.NextFloat() < 0.1f;

            // Цвет (серый по умолчанию, обновится через GridColorConfig в runtime)
            float4 grayColor = new float4(0.5f, 0.5f, 0.5f, 0f);
            // Цвет из конфига (не хардкод)
            Colors[instance] = new URPMaterialPropertyBaseColor { Value = ColorGray };


            // Заполняем буфер карты
            MapBuffer[index] = new GridCellElement
            {
                CellEntity = instance,
                IsOccupiedGround = isWall,
                IsOccupiedUnderground = isWall,
                IsOccupiedSky = false,
                OccupantGround = Entity.Null,
                OccupantUnderground = Entity.Null,
                OccupantSky = Entity.Null,
                IsHighlighted = false
            };
        }
    }
}
