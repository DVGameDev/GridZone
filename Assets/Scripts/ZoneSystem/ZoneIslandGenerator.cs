using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Алгоритмы генерации островов радиации для ZONE режима
/// </summary>
public static class ZoneIslandGenerator
{
    /// <summary>
    /// Генерирует Green острова (примыкают только к Yellow)
    /// </summary>
    public static void GenerateGreenIslands(
        DynamicBuffer<ZoneCellRadiation> radiationBuffer,
        int2 gridSize,
        ZoneRadiationConfig radiationConfig,
        ZoneIslandConfig islandConfig,
        uint seed)
    {
        var random = Unity.Mathematics.Random.CreateFromIndex(seed);

        for (int i = 0; i < radiationBuffer.Length; i++)
        {
            var cell = radiationBuffer[i];

            // Только Yellow клетки
            if (cell.RadiationLevel != radiationConfig.LevelYellow) continue;

            // С вероятностью GreenProbability
            if (random.NextFloat() > islandConfig.GreenProbability) continue;

            // Генерируем остров размера 2-5 клеток
            int islandSize = islandConfig.GreenSizeMin + random.NextInt(islandConfig.GreenSizeMax - islandConfig.GreenSizeMin + 1);
            GenerateIsland(radiationBuffer, gridSize, i, radiationConfig.LevelGreen, islandSize, random);
        }
    }

    /// <summary>
    /// Генерирует Orange острова (любые клетки кроме Red)
    /// </summary>
    public static void GenerateOrangeIslands(
        DynamicBuffer<ZoneCellRadiation> radiationBuffer,
        int2 gridSize,
        ZoneRadiationConfig radiationConfig,
        ZoneIslandConfig islandConfig,
        uint seed)
    {
        var random = Unity.Mathematics.Random.CreateFromIndex(seed + 1);

        for (int i = 0; i < radiationBuffer.Length; i++)
        {
            var cell = radiationBuffer[i];

            // Любые кроме Red
            if (cell.RadiationLevel == radiationConfig.LevelRed) continue;

            if (random.NextFloat() > islandConfig.OrangeProbability) continue;

            int islandSize = islandConfig.OrangeSizeMin + random.NextInt(islandConfig.OrangeSizeMax - islandConfig.OrangeSizeMin + 1);
            GenerateIsland(radiationBuffer, gridSize, i, radiationConfig.LevelOrange, islandSize, random);
        }
    }

    /// <summary>
    /// Генерирует Red острова (примыкают только к Orange)
    /// </summary>
    public static void GenerateRedIslands(
        DynamicBuffer<ZoneCellRadiation> radiationBuffer,
        int2 gridSize,
        ZoneRadiationConfig radiationConfig,
        ZoneIslandConfig islandConfig,
        uint seed)
    {
        var random = Unity.Mathematics.Random.CreateFromIndex(seed + 2);

        for (int i = 0; i < radiationBuffer.Length; i++)
        {
            var cell = radiationBuffer[i];

            // Только Orange клетки
            if (cell.RadiationLevel != radiationConfig.LevelOrange) continue;

            if (random.NextFloat() > islandConfig.RedProbability) continue;

            int islandSize = islandConfig.RedSizeMin + random.NextInt(islandConfig.RedSizeMax - islandConfig.RedSizeMin + 1);
            GenerateIsland(radiationBuffer, gridSize, i, radiationConfig.LevelRed, islandSize, random);
        }
    }

    /// <summary>
    /// Универсальный генератор острова (BFS)
    /// </summary>
    private static void GenerateIsland(
        DynamicBuffer<ZoneCellRadiation> radiationBuffer,
        int2 gridSize,
        int startIndex,
        int newRadiationLevel,
        int maxSize,
        Unity.Mathematics.Random random)
    {
        var queue = new NativeQueue<int>(Allocator.Temp);
        var visited = new NativeHashSet<int>(radiationBuffer.Length, Allocator.Temp);

        queue.Enqueue(startIndex);
        visited.Add(startIndex);

        int placed = 1;

        while (placed < maxSize && queue.Count > 0)
        {
            int currentIndex = queue.Dequeue();
            var currentCell = radiationBuffer[currentIndex];

            // Получаем соседей (Hex)
            var neighbors = GetHexNeighbors(currentCell.GridPos, gridSize);

            foreach (var neighborIndex in neighbors)
            {
                if (visited.Contains(neighborIndex)) continue;

                // Проверяем, подходит ли сосед (не посещен, правильный цвет)
                var neighborCell = radiationBuffer[neighborIndex];
                if (neighborCell.RadiationLevel == newRadiationLevel) continue;

                // Помечаем
                radiationBuffer[neighborIndex] = new ZoneCellRadiation
                {
                    GridPos = neighborCell.GridPos,
                    CellEntity = neighborCell.CellEntity,
                    RadiationLevel = newRadiationLevel,
                    IsVisited = false
                };

                visited.Add(neighborIndex);
                queue.Enqueue(neighborIndex);
                placed++;

                if (placed >= maxSize) break;
            }

            if (placed >= maxSize) break;
        }

        queue.Dispose();
        visited.Dispose();
    }

    /// <summary>
    /// Получить индексы 6 соседних hex клеток
    /// </summary>
    /// <summary>
    /// Получить индексы 6 соседних hex клеток (Burst-совместимо)
    /// </summary>
    /// <summary>
    /// Получить индексы 6 соседних hex клеток (Burst-совместимо)
    /// </summary>
    private static NativeList<int> GetHexNeighbors(int2 pos, int2 gridSize)
    {
        var neighbors = new NativeList<int>(6, Allocator.Temp);

        // 🔥 Burst-совместимые направления (struct вместо массива)
        // E, SE, SW, W, NW, NE
        int2 dirE = new int2(1, 0);
        int2 dirSE = new int2(1, -1);
        int2 dirSW = new int2(0, -1);
        int2 dirW = new int2(-1, 0);
        int2 dirNW = new int2(-1, 1);
        int2 dirNE = new int2(0, 1);

        CheckAndAddNeighbor(pos + dirE, gridSize, ref neighbors);
        CheckAndAddNeighbor(pos + dirSE, gridSize, ref neighbors);
        CheckAndAddNeighbor(pos + dirSW, gridSize, ref neighbors);
        CheckAndAddNeighbor(pos + dirW, gridSize, ref neighbors);
        CheckAndAddNeighbor(pos + dirNW, gridSize, ref neighbors);
        CheckAndAddNeighbor(pos + dirNE, gridSize, ref neighbors);

        return neighbors;
    }

    private static void CheckAndAddNeighbor(int2 neighborPos, int2 gridSize, ref NativeList<int> neighbors)
    {
        if (HexGridUtils.IsHexInBounds(neighborPos, gridSize))
        {
            int index = HexGridUtils.HexToIndex(neighborPos, gridSize);
            neighbors.Add(index);
        }
    }


}
