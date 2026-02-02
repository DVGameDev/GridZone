using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Полная математика Flat-Top Hex Grid (Axial координаты q,r)
/// Совместимо с существующей Quad системой
/// </summary>
public static class HexGridUtils
{
    /// <summary>
    /// Axial (q,r) → World позиция (Flat-Top)
    /// </summary>
    public static float3 HexAxialToWorld(int2 axial, float hexSize)
    {
        float x = hexSize * (3f / 2f * axial.x);
        float z = hexSize * (math.sqrt(3f) * axial.y + math.sqrt(3f) / 2f * axial.x);
        return new float3(x, 0, z);
    }

    /// <summary>
    /// World → Axial (q,r) координаты (Flat-Top)
    /// </summary>
    public static int2 WorldToHexAxial(float3 worldPos, float hexSize)
    {
        float q = (2f / 3f * worldPos.x) / hexSize;
        float r = (-1f / 3f * worldPos.x + math.sqrt(3f) / 3f * worldPos.z) / hexSize;

        return HexRound(q, r);
    }

    /// <summary>
    /// Округление дробных hex координат до целых (Axial)
    /// </summary>
    private static int2 HexRound(float q, float r)
    {
        float s = -q - r;

        int rq = (int)math.round(q);
        int rr = (int)math.round(r);
        int rs = (int)math.round(s);

        float qDiff = math.abs(rq - q);
        float rDiff = math.abs(rr - r);
        float sDiff = math.abs(rs - s);

        if (qDiff > rDiff && qDiff > sDiff)
            rq = -rr - rs;
        else if (rDiff > sDiff)
            rr = -rq - rs;

        return new int2(rq, rr);
    }

    /// <summary>
    /// Получить 6 соседей hex клетки (Flat-Top Axial)
    /// Порядок: E, SE, SW, W, NW, NE
    /// </summary>
    public static void GetHexNeighbors(int2 axial, ref NativeList<int2> neighbors)
    {
        neighbors.Clear();

        // Направления для Flat-Top в Axial
        neighbors.Add(axial + new int2(1, 0));   // E (восток)
        neighbors.Add(axial + new int2(1, -1));  // SE (юго-восток)
        neighbors.Add(axial + new int2(0, -1));  // SW (юго-запад)
        neighbors.Add(axial + new int2(-1, 0));  // W (запад)
        neighbors.Add(axial + new int2(-1, 1));  // NW (северо-запад)
        neighbors.Add(axial + new int2(0, 1));   // NE (северо-восток)
    }

    /// <summary>
    /// Расстояние между двумя hex клетками (Axial)
    /// </summary>
    public static int HexDistance(int2 a, int2 b)
    {
        int q = a.x - b.x;
        int r = a.y - b.y;
        return (math.abs(q) + math.abs(q + r) + math.abs(r)) / 2;
    }

    /// <summary>
    /// Проверка, находится ли hex в пределах сетки
    /// </summary>
    public static bool IsHexInBounds(int2 axial, int2 gridSize)
    {
        return axial.x >= 0 && axial.x < gridSize.x &&
               axial.y >= 0 && axial.y < gridSize.y;
    }

    /// <summary>
    /// Линейный индекс hex клетки в буфере (совместимо с Quad)
    /// </summary>
    public static int HexToIndex(int2 axial, int2 gridSize)
    {
        return axial.y * gridSize.x + axial.x;
    }

    /// <summary>
    /// Получить все hex клетки в радиусе (для движения/AoE)
    /// Использует кубический алгоритм перебора
    /// </summary>
    public static void GetHexesInRange(int2 center, int range, int2 gridSize, ref NativeList<int2> results)
    {
        results.Clear();

        // Кубический алгоритм для hex сетки
        for (int dq = -range; dq <= range; dq++)
        {
            int dr1 = math.max(-range, -dq - range);
            int dr2 = math.min(range, -dq + range);

            for (int dr = dr1; dr <= dr2; dr++)
            {
                int2 hex = center + new int2(dq, dr);

                // Проверяем границы сетки
                if (hex.x >= 0 && hex.x < gridSize.x && hex.y >= 0 && hex.y < gridSize.y)
                {
                    results.Add(hex);
                }
            }
        }
    }

    /// <summary>
    /// Конвертация Axial → World с учетом высоты слоя
    /// </summary>
    public static float3 GetHexWorldPosition(int2 axial, float hexSize, UnitLayer layer, GridConfig config)
    {
        float3 basePos = HexAxialToWorld(axial, hexSize);

        float posY = config.HeightGround;
        switch (layer)
        {
            case UnitLayer.Sky:
                posY = config.HeightSky;
                break;
            case UnitLayer.Underground:
                posY = config.HeightUnderground;
                break;
        }

        return new float3(basePos.x, posY, basePos.z);
    }

    /// <summary>
    /// Линия между двумя hex клетками (линейная интерполяция)
    /// </summary>
    public static void GetHexLine(int2 start, int2 end, ref NativeList<int2> results)
    {
        results.Clear();

        int distance = HexDistance(start, end);
        if (distance == 0)
        {
            results.Add(start);
            return;
        }

        for (int i = 0; i <= distance; i++)
        {
            float t = (float)i / distance;
            float2 lerped = math.lerp(new float2(start.x, start.y), new float2(end.x, end.y), t);
            results.Add(HexRound(lerped.x, lerped.y));
        }
    }

    /// <summary>
    /// Получить hex клетки в форме кольца на заданном расстоянии
    /// </summary>
    public static void GetHexRing(int2 center, int radius, int2 gridSize, ref NativeList<int2> results)
    {
        results.Clear();

        if (radius == 0)
        {
            if (IsHexInBounds(center, gridSize))
                results.Add(center);
            return;
        }

        // Начинаем с hex в направлении (-radius, 0)
        int2 current = center + new int2(-radius, 0);

        // 6 направлений для обхода кольца
        int2[] directions = new int2[]
        {
            new int2(1, 0),   // E
            new int2(1, -1),  // SE
            new int2(0, -1),  // SW
            new int2(-1, 0),  // W
            new int2(-1, 1),  // NW
            new int2(0, 1)    // NE
        };

        for (int i = 0; i < 6; i++)
        {
            for (int j = 0; j < radius; j++)
            {
                if (IsHexInBounds(current, gridSize))
                    results.Add(current);

                current += directions[i];
            }
        }
    }
}
