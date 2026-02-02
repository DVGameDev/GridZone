using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Jobs;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct GridSpawnerSystem : ISystem
{
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var configQuery = SystemAPI.QueryBuilder().WithAll<GridConfig>().Build();
        if (!configQuery.IsEmpty) return;

        var query = SystemAPI.QueryBuilder().WithAll<GridSpawnerComponent>().Build();
        if (query.IsEmpty) return;

        var spawnerEntities = query.ToEntityArray(Allocator.Temp);
        var spawnerComponents = query.ToComponentDataArray<GridSpawnerComponent>(Allocator.Temp);

        if (spawnerEntities.Length > 0)
        {
            var spawnerEntity = spawnerEntities[0];
            var spawnerData = spawnerComponents[0];
            if (spawnerData.Layout != GridLayoutType.Quad)
            {
                spawnerEntities.Dispose();
                spawnerComponents.Dispose();
                return;
            }

            int width = spawnerData.GridSize.x;
            int height = spawnerData.GridSize.y;
            int totalCells = width * height;

            // 1. Создаем карту
            var mapEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(mapEntity, "GridMap");
            state.EntityManager.AddComponentData(mapEntity, new GridMapTag { Size = spawnerData.GridSize });
            state.EntityManager.AddBuffer<GridCellElement>(mapEntity);

            // 2. Создаем конфиг
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
                VisualMode = spawnerData.VisualMode
            });

            // 🔥 GridColorConfig уже на spawnerEntity благодаря Baker
            // Просто НЕ удаляем его при удалении GridSpawnerComponent

            // 3. Инстанцируем визуальные префабы
            var instances = new NativeArray<Entity>(totalCells, Allocator.TempJob);
            state.EntityManager.Instantiate(spawnerData.PrefabEntity, instances);
            state.EntityManager.AddComponent<GridCoordinates>(instances);
            state.EntityManager.AddComponent<URPMaterialPropertyBaseColor>(instances);

            // 4. Получаем буфер
            var mapBuffer = state.EntityManager.GetBuffer<GridCellElement>(mapEntity);
            mapBuffer.ResizeUninitialized(totalCells);

            // 🔥 ОПТИМИЗАЦИЯ: Параллельная инициализация через Job
            // 🔥 Читаем GridColorConfig ДО создания Job
            var colors = SystemAPI.GetSingleton<GridColorConfig>();

            var initJob = new InitializeGridJob
            {
                Instances = instances,
                GridSize = new int2(width, height),
                Spacing = spawnerData.Spacing,
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

            // Удаляем только GridSpawnerComponent, оставляя GridColorConfig как синглтон
            state.EntityManager.RemoveComponent<GridSpawnerComponent>(spawnerEntity);
            state.EntityManager.SetName(spawnerEntity, "GridColorConfig");
        }

        spawnerEntities.Dispose();
        spawnerComponents.Dispose();
    }


    /// <summary>
    /// Burst-компилируемая Job для параллельной инициализации грида
    /// </summary>
    [BurstCompile]
    private struct InitializeGridJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Entity> Instances;
        [ReadOnly] public int2 GridSize;
        [ReadOnly] public float Spacing;
        [ReadOnly] public uint RandomSeed;

        [NativeDisableParallelForRestriction] public ComponentLookup<LocalTransform> Transforms;
        [NativeDisableParallelForRestriction] public ComponentLookup<GridCoordinates> Coordinates;
        [NativeDisableParallelForRestriction] public ComponentLookup<URPMaterialPropertyBaseColor> Colors;
        [NativeDisableParallelForRestriction] public DynamicBuffer<GridCellElement> MapBuffer;
        [ReadOnly] public float4 ColorGray; // 🔥 Цвет из конфига

        public void Execute(int index)
        {
            // Вычисляем координаты из линейного индекса
            int x = index / GridSize.y;
            int y = index % GridSize.y;

            var instance = Instances[index];

            // Позиция
            float3 pos = new float3((x) * Spacing, 0, y * Spacing);
            Transforms[instance] = LocalTransform.FromPositionRotation(pos, quaternion.identity);

            // Координаты
            Coordinates[instance] = new GridCoordinates { Value = new int2(x, y) };

            // Генерация препятствий (детерминированный рандом)
            var random = Unity.Mathematics.Random.CreateFromIndex((uint)index + RandomSeed);
            bool isWall = random.NextFloat() < 0.1f;

            // Цвет

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
