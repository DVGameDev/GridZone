using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Хелпер для действий юнитов: поворот, движение, валидация
/// Поддержка Quad + HexFlatTop
/// </summary>
public static class UnitActionHelper
{
    /// <summary>
    /// Пытается повернуть юнита (только Quad)
    /// </summary>
    

    /// <summary>
    /// Универсальное движение
    /// </summary>
    public static bool TryMoveUnit(
        EntityManager em,
        Entity unit,
        int2 oldPos,
        int2 targetPos,
        int2 currentSize,
        int2 currentFacing,
        float spacing,
        DynamicBuffer<GridCellElement> map,
        int2 gridSize,
        UnitLayer layer,
        GridConfig config)
    {
        if (config.Layout == GridLayoutType.HexFlatTop)
            return TryMoveUnit_Hex(em, unit, oldPos, targetPos, spacing, map, gridSize, layer, config);
        else
            return TryMoveUnit_Quad(em, unit, oldPos, targetPos, currentSize, currentFacing, spacing, map, gridSize, layer, config);
    }

    private static bool TryMoveUnit_Hex(
        EntityManager em,
        Entity unit,
        int2 oldPos,
        int2 targetPos,
        float spacing,
        DynamicBuffer<GridCellElement> map,
        int2 gridSize,
        UnitLayer layer,
        GridConfig config)
    {
        // 🔥 ИСПРАВЛЕНО: используем HexGridUtils для axial координат
        if (!HexGridUtils.IsHexInBounds(targetPos, gridSize))
            return false;

        // 🔥 ИСПРАВЛЕНО: используем HexGridUtils для Hex координат
        int oldIndex = HexGridUtils.HexToIndex(oldPos, gridSize);
        int targetIndex = HexGridUtils.HexToIndex(targetPos, gridSize);

        var oldCell = map[oldIndex];
        var targetCell = map[targetIndex];

        if (GridUtils.IsCellOccupied(targetCell, layer))
            return false;

        // Освобождаем старую
        switch (layer)
        {
            case UnitLayer.Ground:
                oldCell.IsOccupiedGround = false;
                oldCell.OccupantGround = Entity.Null; break;
            case UnitLayer.Sky:
                oldCell.IsOccupiedSky = false;
                oldCell.OccupantSky = Entity.Null; break;
            case UnitLayer.Underground:
                oldCell.IsOccupiedUnderground = false;
                oldCell.OccupantUnderground = Entity.Null; break;
        }
        map[oldIndex] = oldCell;

        // Занимаем новую
        switch (layer)
        {
            case UnitLayer.Ground:
                targetCell.IsOccupiedGround = true;
                targetCell.OccupantGround = unit; break;
            case UnitLayer.Sky:
                targetCell.IsOccupiedSky = true;
                targetCell.OccupantSky = unit; break;
            case UnitLayer.Underground:
                targetCell.IsOccupiedUnderground = true;
                targetCell.OccupantUnderground = unit; break;
        }
        map[targetIndex] = targetCell;

        em.SetComponentData(unit, new GridCoordinates { Value = targetPos });

        float3 targetWorldPos = HexGridUtils.GetHexWorldPosition(targetPos, spacing, layer, config);

        var cmd = em.GetComponentData<MoveCommand>(unit);
        cmd.IsMoving = true;
        cmd.TargetPosition = targetWorldPos;
        em.SetComponentData(unit, cmd);

        return true;
    }

    private static bool TryMoveUnit_Quad(
        EntityManager em,
        Entity unit,
        int2 oldPos,
        int2 targetPos,
        int2 currentSize,
        int2 currentFacing,
        float spacing,
        DynamicBuffer<GridCellElement> map,
        int2 gridSize,
        UnitLayer layer,
        GridConfig config)
    {
        // 🔥 ВСЯ ОРИГИНАЛЬНАЯ ЛОГИКА QUAD ОСТАЕТСЯ НЕИЗМЕННОЙ
        int2 dir = targetPos - oldPos;
        int2 newFacing = currentFacing;

        if (config.FacingMode != UnitFacingMode.Fixed && !dir.Equals(int2.zero))
        {
            if (math.abs(dir.x) > math.abs(dir.y))
                newFacing = dir.x > 0 ? new int2(1, 0) : new int2(-1, 0);
            else
                newFacing = dir.y > 0 ? new int2(0, 1) : new int2(0, -1);
        }

        bool currentIsVertical = (currentFacing.y != 0);
        bool newIsVertical = (newFacing.y != 0);
        int2 newSize = currentSize;
        bool needsSwap = (currentIsVertical != newIsVertical) && (currentSize.x != currentSize.y);

        if (needsSwap)
            newSize = new int2(currentSize.y, currentSize.x);

        GridUtils.UpdateMapOccupancy(map, gridSize, oldPos, currentSize, false, Entity.Null, layer);
        GridUtils.UpdateMapOccupancy(map, gridSize, targetPos, newSize, true, unit, layer);

        em.SetComponentData(unit, new GridCoordinates { Value = targetPos });
        if (needsSwap)
            em.SetComponentData(unit, new UnitSize { Value = newSize });
        if (!newFacing.Equals(currentFacing))
            em.SetComponentData(unit, new UnitFacing { Value = newFacing });

        float3 targetWorldPos = GridUtils.GridToWorld(targetPos, spacing, config.Layout, layer, config);

        var cmd = em.GetComponentData<MoveCommand>(unit);
        cmd.IsMoving = true;
        cmd.TargetPosition = targetWorldPos;
        em.SetComponentData(unit, cmd);

        return true;
    }

    /// <summary>
    /// Универсальная проверка коллизий
    /// </summary>
    public static bool CanUnitFitAt(
    EntityManager em,
    Entity unit,
    int2 anchor,
    int2 size,
    DynamicBuffer<GridCellElement> map,
    int2 gridSize,
    UnitLayer layer,
    GridConfig config)  // 🔥 ПАРАМЕТР
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int2 cellPos;
                if (config.Layout == GridLayoutType.HexFlatTop)
                    cellPos = anchor;
                else
                    cellPos = new int2(anchor.x + x, anchor.y - y);

                // 🔥 ИСПРАВЛЕНО: проверяем bounds в зависимости от layout
                bool inBounds;
                if (config.Layout == GridLayoutType.HexFlatTop)
                    inBounds = HexGridUtils.IsHexInBounds(cellPos, gridSize);
                else
                    inBounds = GridUtils.IsInBounds(cellPos, gridSize);
                
                if (!inBounds)
                    return false;

                // 🔥 ИСПРАВЛЕНО: добавлена проверка layout
                int index;
                if (config.Layout == GridLayoutType.HexFlatTop)
                    index = HexGridUtils.HexToIndex(cellPos, gridSize);
                else
                    index = GridUtils.GridToIndex(cellPos, gridSize);
                var cell = map[index];
                Entity occupant = GridUtils.GetOccupant(cell, layer);

                if (GridUtils.IsCellOccupied(cell, layer) && occupant != unit)
                    return false;
            }
        }
        return true;
    }


    /// <summary>
    /// Проверка блокировки курсора
    /// </summary>
    public static bool IsCursorBlocked(
     Entity selectedUnit,
     int2 hitCoords,
     NativeList<int2> cursorOffsets,
     DynamicBuffer<GridCellElement> mapBuffer,
     int2 gridSize,
     UnitLayer layer,
     GridConfig config)
    {
        foreach (var offset in cursorOffsets.AsArray())
        {
            int2 targetPos = hitCoords + offset;

            // 🔥 ИСПРАВЛЕНО: проверяем bounds в зависимости от layout
            bool inBounds;
            if (config.Layout == GridLayoutType.HexFlatTop)
                inBounds = HexGridUtils.IsHexInBounds(targetPos, gridSize);
            else
                inBounds = GridUtils.IsInBounds(targetPos, gridSize);
                
            if (!inBounds)
                return true;

            int index;
            if (config.Layout == GridLayoutType.HexFlatTop)
                index = HexGridUtils.HexToIndex(targetPos, gridSize);
            else
                index = GridUtils.GridToIndex(targetPos, gridSize);

            var cell = mapBuffer[index];

            if ((GridUtils.IsCellOccupied(cell, layer) && GridUtils.GetOccupant(cell, layer) != selectedUnit)
                || !cell.IsHighlighted)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Универсальный поворот юнита (Quad + Hex)
    /// </summary>
    public static bool TryRotateUnit(
        EntityManager em,
        Entity unit,
        int2 currentPos,
        int2 targetPos,
        int2 currentSize,
        int2 currentFacing,
        float spacing,
        DynamicBuffer<GridCellElement> map,
        int2 gridSize,
        UnitLayer layer,
        GridConfig config)
    {
        if (config.Layout == GridLayoutType.HexFlatTop)
        {
            return TryRotateUnit_Hex(em, unit, currentPos, targetPos, spacing, layer, config);
        }
        else
        {
            return TryRotateUnit_Quad(em, unit, currentPos, targetPos, currentSize, currentFacing, spacing, map, gridSize, layer, config);
        }
    }

    /// <summary>
    /// Поворот Hex юнита (плавный, в сторону клика)
    /// </summary>
    private static bool TryRotateUnit_Hex(
        EntityManager em,
        Entity unit,
        int2 currentPos,
        int2 targetPos,
        float spacing,
        UnitLayer layer,
        GridConfig config)
    {
        // Направление к точке клика
        int2 dir = targetPos - currentPos;
        if (dir.Equals(int2.zero)) return false;

        // Hex поворот - плавный (без snap к 4 направлениям)
        float3 currentWorldPos = HexGridUtils.GetHexWorldPosition(currentPos, spacing, layer, config);
        float3 targetWorldPos = HexGridUtils.GetHexWorldPosition(targetPos, spacing, layer, config);

        float3 lookDir = targetWorldPos - currentWorldPos;
        lookDir.y = 0; // Игнорируем Y (высоту)

        if (math.lengthsq(lookDir) < 0.001f) return false;

        // Новая rotation
        quaternion newRotation = quaternion.LookRotation(lookDir, math.up());

        // Применяем
        var transform = em.GetComponentData<LocalTransform>(unit);
        transform.Rotation = newRotation;
        em.SetComponentData(unit, transform);

        // Обновляем facing (для логики, если используется)
        float3 forward = math.normalize(lookDir);
        int2 newFacing = new int2((int)math.round(forward.x), (int)math.round(forward.z));
        if (!newFacing.Equals(int2.zero))
        {
            em.SetComponentData(unit, new UnitFacing { Value = newFacing });
        }

        return true;
    }

    /// <summary>
    /// Поворот Quad юнита (оригинальная логика с swap размеров)
    /// </summary>
    private static bool TryRotateUnit_Quad(
        EntityManager em,
        Entity unit,
        int2 currentPos,
        int2 targetPos,
        int2 currentSize,
        int2 currentFacing,
        float spacing,
        DynamicBuffer<GridCellElement> map,
        int2 gridSize,
        UnitLayer layer,
        GridConfig config)
    {
        // 🔥 ОРИГИНАЛЬНЫЙ КОД QUAD БЕЗ ИЗМЕНЕНИЙ
        int2 dir = targetPos - currentPos;
        if (dir.Equals(int2.zero)) return false;

        int2 newFacing;
        if (math.abs(dir.x) > math.abs(dir.y))
            newFacing = dir.x > 0 ? new int2(1, 0) : new int2(-1, 0);
        else
            newFacing = dir.y > 0 ? new int2(0, 1) : new int2(0, -1);

        if (newFacing.Equals(currentFacing)) return false;

        bool currentIsVertical = (currentFacing.y != 0);
        bool newIsVertical = (newFacing.y != 0);
        int2 newSize = currentSize;
        bool needsSwap = (currentIsVertical != newIsVertical) && (currentSize.x != currentSize.y);

        if (needsSwap)
            newSize = new int2(currentSize.y, currentSize.x);

        if (!CanUnitFitAt(em, unit, currentPos, newSize, map, gridSize, layer, config))
            return false;

        if (needsSwap)
        {
            GridUtils.UpdateMapOccupancy(map, gridSize, currentPos, currentSize, false, Entity.Null, layer);
            GridUtils.UpdateMapOccupancy(map, gridSize, currentPos, newSize, true, unit, layer);
            em.SetComponentData(unit, new UnitSize { Value = newSize });
        }

        em.SetComponentData(unit, new UnitFacing { Value = newFacing });

        float3 gridWorldPos = GridUtils.GridToWorld(currentPos, config.Spacing, config.Layout, layer, config);
        float3 lookDir = new float3(newFacing.x, 0, newFacing.y);
        quaternion rotation = quaternion.LookRotation(lookDir, math.up());

        var transform = em.GetComponentData<LocalTransform>(unit);
        transform.Rotation = rotation;
        em.SetComponentData(unit, transform);

        return true;
    }

}
