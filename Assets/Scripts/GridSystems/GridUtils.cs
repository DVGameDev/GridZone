using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Утилиты для работы с Grid, слоями юнитов и координатами
/// </summary>
public static class GridUtils
{
    /// <summary>
    /// Проверяет, занята ли клетка на указанном слое
    /// </summary>
    public static bool IsCellOccupied(GridCellElement cell, UnitLayer layer)
    {
        switch (layer)
        {
            case UnitLayer.Underground: return cell.IsOccupiedUnderground;
            case UnitLayer.Ground: return cell.IsOccupiedGround;
            case UnitLayer.Sky: return cell.IsOccupiedSky;
            default: return false;
        }
    }

    /// <summary>
    /// Возвращает юнита, занимающего клетку на указанном слое
    /// </summary>
    public static Entity GetOccupant(GridCellElement cell, UnitLayer layer)
    {
        switch (layer)
        {
            case UnitLayer.Underground: return cell.OccupantUnderground;
            case UnitLayer.Ground: return cell.OccupantGround;
            case UnitLayer.Sky: return cell.OccupantSky;
            default: return Entity.Null;
        }
    }

    /// <summary>
    /// Получает текущий размер юнита (с учетом поворота)
    /// </summary>
    public static int2 GetCurrentUnitSize(EntityManager em, Entity unit)
    {
        if (!em.HasComponent<UnitSize>(unit))
            return new int2(1, 1);

        return em.GetComponentData<UnitSize>(unit).Value;
    }

    /// <summary>
    /// Конвертирует grid координаты в мировые с центрированием по размеру юнита
    /// </summary>
    public static float3 GetWorldPositionForGridAnchor(int2 gridAnchor, int2 size, float spacing, UnitLayer layer, GridConfig config)
    {
        // Базовая позиция якоря (левый верхний угол)
        float3 basePos = new float3(gridAnchor.x * spacing, 0, gridAnchor.y * spacing);

        // Смещение к центру (Size учитывается)
        float offsetX = (size.x - 1) * spacing * 0.5f;
        float offsetZ = -(size.y - 1) * spacing * 0.5f;

        // Высота по слою
        float posY = config.HeightGround;
        switch (layer)
        {
            case UnitLayer.Sky: posY = config.HeightSky; break;
            case UnitLayer.Underground: posY = config.HeightUnderground; break;
        }

        return new float3(basePos.x + offsetX, posY, basePos.z + offsetZ);
    }

    /// <summary>
    /// Обновляет занятость клеток на карте для юнита указанного размера
    /// </summary>
    public static void UpdateMapOccupancy(DynamicBuffer<GridCellElement> map, int2 gridSize, int2 anchor,
        int2 size, bool occupy, Entity unit, UnitLayer layer)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int tx = anchor.x + x;
                int ty = anchor.y - y;
                if (tx < 0 || tx >= gridSize.x || ty < 0 || ty >= gridSize.y) continue;

                int index = tx * gridSize.y + ty;
                var cell = map[index];

                switch (layer)
                {
                    case UnitLayer.Ground:
                        cell.IsOccupiedGround = occupy;
                        cell.OccupantGround = occupy ? unit : Entity.Null;
                        break;
                    case UnitLayer.Sky:
                        cell.IsOccupiedSky = occupy;
                        cell.OccupantSky = occupy ? unit : Entity.Null;
                        break;
                    case UnitLayer.Underground:
                        cell.IsOccupiedUnderground = occupy;
                        cell.OccupantUnderground = occupy ? unit : Entity.Null;
                        break;
                }

                map[index] = cell;
            }
        }
    }

    /// <summary>
    /// Конвертирует мировую позицию в grid координаты
    /// </summary>
    public static int2 GetGridPosFromWorld(float3 pos, float spacing)
    {
        return new int2((int)math.round(pos.x / spacing), (int)math.round(pos.z / spacing));
    }

    /// <summary>
    /// Предсказывает размер юнита при движении в указанном направлении
    /// (с учетом поворота для многоклеточных юнитов)
    /// </summary>
    public static int2 PredictSizeForDirection(int2 currentSize, int2 currentFacing, int2 unitPos, int2 targetPos, UnitFacingMode facingMode)
    {
        if (facingMode == UnitFacingMode.Fixed)
            return currentSize;

        // Определяем направление движения
        int2 dir = targetPos - unitPos;
        if (dir.Equals(int2.zero))
            return currentSize;

        // Определяем новый Facing
        int2 newFacing;
        if (math.abs(dir.x) > math.abs(dir.y))
            newFacing = dir.x > 0 ? new int2(1, 0) : new int2(-1, 0);
        else
            newFacing = dir.y > 0 ? new int2(0, 1) : new int2(0, -1);

        // Если Facing меняется с вертикального на горизонтальный или наоборот — свапаем Size
        bool currentIsVertical = (currentFacing.y != 0);
        bool newIsVertical = (newFacing.y != 0);

        if (currentIsVertical != newIsVertical && currentSize.x != currentSize.y)
            return new int2(currentSize.y, currentSize.x);

        return currentSize;
    }
    /// <summary>
    /// Универсальная конвертация World → Grid координаты (Quad или Hex)
    /// </summary>
    public static int2 WorldToGrid(float3 worldPos, float spacing, GridLayoutType layout)
    {
        if (layout == GridLayoutType.HexFlatTop)
        {
            return HexGridUtils.WorldToHexAxial(worldPos, spacing);
        }
        else // Quad
        {
            return GetGridPosFromWorld(worldPos, spacing);
        }
    }
    

    /// <summary>
    /// Универсальная конвертация Grid → World
    /// </summary>
    public static float3 GridToWorld(int2 gridPos, float spacing, GridLayoutType layout, UnitLayer layer, GridConfig config)
    {
        if (layout == GridLayoutType.HexFlatTop)
            return HexGridUtils.GetHexWorldPosition(gridPos, spacing, layer, config);
        else
            return GetWorldPositionForGridAnchor(gridPos, new int2(1, 1), spacing, layer, config);
    }

    /// <summary>
    /// Линейный индекс
    /// </summary>
    public static int GridToIndex(int2 pos, int2 gridSize)
    {
        return pos.x * gridSize.y + pos.y;
    }

    public static bool IsInBounds(int2 pos, int2 gridSize)
    {
        return pos.x >= 0 && pos.x < gridSize.x && pos.y >= 0 && pos.y < gridSize.y;
    }




}
