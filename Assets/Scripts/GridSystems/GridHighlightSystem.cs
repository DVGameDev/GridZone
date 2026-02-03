using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(SelectionSystem))]
public partial class GridHighlightSystem : SystemBase
{
    private Entity _lastSelectedUnit = Entity.Null;
    private int2 _lastUnitGridPos = new int2(-9999, -9999);
    private InteractionMode _lastMode = InteractionMode.None;

    // Кэш подсвеченных клеток
    private NativeList<Entity> _highlightedCells;

   

    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<GridMapTag>();
        RequireForUpdate<ActiveUnitComponent>();

        _highlightedCells = new NativeList<Entity>(Allocator.Persistent);
    }

    protected override void OnDestroy()
    {
        if (_highlightedCells.IsCreated)
            _highlightedCells.Dispose();
    }

    protected override void OnUpdate()
    {
        var selectionState = SystemAPI.GetSingleton<ActiveUnitComponent>();
        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        var mapBuffer = EntityManager.GetBuffer<GridCellElement>(mapEntity);
        var config = SystemAPI.GetSingleton<GridConfig>();
        var colors = SystemAPI.GetSingleton<GridColorConfig>();
        int2 gridSize = config.GridSize;

        Entity selectedUnit = selectionState.Unit;
        bool unitValid = selectedUnit != Entity.Null && EntityManager.Exists(selectedUnit);

        int2 currentGridPos = new int2(-9999, -9999);
        UnitLayer currentLayer = UnitLayer.Ground;
        int2 unitSize = new int2(1, 1);
        int2 unitFacing = new int2(0, 1);

        if (unitValid)
        {
            //Debug.Log($"=== UNIT DEBUG ===");
           // Debug.Log($"SelectedUnit Index: {selectedUnit.Index}");

            if (EntityManager.HasComponent<GridCoordinates>(selectedUnit))
            {
                currentGridPos = EntityManager.GetComponentData<GridCoordinates>(selectedUnit).Value;
                //Debug.Log($"GridCoordinates: {currentGridPos}");
            }
            else
            {
               // Debug.LogError("NO GridCoordinates on unit!");
                return;
            }

            //Debug.Log($"Unit WorldPos: {EntityManager.GetComponentData<LocalTransform>(selectedUnit).Position}");
            //Debug.Log($"Config GridSize: {config.GridSize}");
            //Debug.Log($"Config Layout: {config.Layout}");
            //Debug.Log($"=================");
        


        if (EntityManager.HasComponent<UnitLayerData>(selectedUnit))
                currentLayer = EntityManager.GetComponentData<UnitLayerData>(selectedUnit).Value;

            if (EntityManager.HasComponent<UnitSize>(selectedUnit))
                unitSize = EntityManager.GetComponentData<UnitSize>(selectedUnit).Value;

            if (EntityManager.HasComponent<UnitFacing>(selectedUnit))
                unitFacing = EntityManager.GetComponentData<UnitFacing>(selectedUnit).Value;
        }


        bool needRedraw =
            (selectedUnit != _lastSelectedUnit) ||
            (unitValid && !currentGridPos.Equals(_lastUnitGridPos)) ||
            (selectionState.Mode != _lastMode);

        if (!needRedraw) return;

        _lastSelectedUnit = selectedUnit;
        _lastUnitGridPos = currentGridPos;
        _lastMode = selectionState.Mode;

        bool isZoneMode = SystemAPI.HasSingleton<ZoneModeTag>();
        bool showObstacles = (selectionState.Mode == InteractionMode.Move) && !isZoneMode;


        // Очищаем только старые подсвеченные клетки
        ClearPreviousHighlights(mapBuffer, gridSize, currentLayer, showObstacles, colors);

        if (unitValid)
        {
            if (selectionState.Mode == InteractionMode.Move && !isZoneMode && EntityManager.HasComponent<UnitStats>(selectedUnit))
            {
                int range = EntityManager.GetComponentData<UnitStats>(selectedUnit).MoveRange;
                HighlightMovementRange(mapBuffer, gridSize, currentGridPos, range, unitSize, selectedUnit, currentLayer, colors);
            }
            else if (selectionState.Mode == InteractionMode.Effect && EntityManager.HasComponent<UnitEffectData>(selectedUnit))
            {
                var unitEffectData = EntityManager.GetComponentData<UnitEffectData>(selectedUnit);
                if (EntityManager.Exists(unitEffectData.EffectEntity) &&
                    EntityManager.HasComponent<EffectShapeData>(unitEffectData.EffectEntity))
                {
                    var shapeData = EntityManager.GetComponentData<EffectShapeData>(unitEffectData.EffectEntity);
                    HighlightAim(mapBuffer, gridSize, currentGridPos, unitSize, unitFacing, shapeData.AimShape, shapeData.EffectShape, colors);
                }
            }
        }
    }


    /// <summary>
    /// Очищает ТОЛЬКО ранее подсвеченные клетки (инкрементальное обновление)
    /// </summary>
    private void ClearPreviousHighlights(DynamicBuffer<GridCellElement> buffer, int2 gridSize, UnitLayer viewerLayer, bool showObstacles, GridColorConfig colors)
    {
        var config = SystemAPI.GetSingleton<GridConfig>();
        
        // 🔥 Проверяем ZONE режим
        bool isZoneMode = SystemAPI.HasSingleton<ZoneModeTag>();
        
        foreach (var cellEntity in _highlightedCells)
        {
            if (!EntityManager.Exists(cellEntity)) continue;

            var coords = EntityManager.GetComponentData<GridCoordinates>(cellEntity);
            
            // 🔥 ИСПРАВЛЕНИЕ: используем правильную индексацию в зависимости от layout
            int index;
            if (config.Layout == GridLayoutType.HexFlatTop)
                index = HexGridUtils.HexToIndex(coords.Value, gridSize);
            else
                index = GridUtils.GridToIndex(coords.Value, gridSize);

            if (index >= 0 && index < buffer.Length)
            {
                var cell = buffer[index];
                cell.IsHighlighted = false;
                buffer[index] = cell;

                // 🔥 УНИВЕРСАЛЬНАЯ СИСТЕМА: проверяем кастомный цвет
                float4 col;

                if (EntityManager.HasComponent<CellCustomColor>(cellEntity))
                {
                    // У клетки есть кастомный базовый цвет (радиация, зоны и т.д.)
                    col = EntityManager.GetComponentData<CellCustomColor>(cellEntity).BaseColor;
                }
                else
                {
                    // Стандартная логика: серый или черный
                    col = colors.ColorGray;
                    if (showObstacles && IsCellOccupied(cell, viewerLayer))
                        col = colors.ColorBlack;
                }

                EntityManager.SetComponentData(cellEntity, new URPMaterialPropertyBaseColor { Value = col });
            }
        }
        _highlightedCells.Clear();
    }




    private void HighlightAim(DynamicBuffer<GridCellElement> buffer, int2 gridSize, int2 anchor, int2 size, int2 facing, AimShapeConfig aimShape, EffectShapeConfig effectShape, GridColorConfig colors)
    {
        var config = SystemAPI.GetSingleton<GridConfig>();
        NativeList<int2> offsets = new NativeList<int2>(Allocator.Temp);
        GridShapeHelper.GetAimOffsets(aimShape, effectShape, size, facing, ref offsets);

        foreach (var offset in offsets)
        {
            int2 pos = anchor + offset;
            if (pos.x >= 0 && pos.x < gridSize.x && pos.y >= 0 && pos.y < gridSize.y)
            {
                // 🔥 ИСПРАВЛЕНИЕ: используем правильную индексацию
                int index;
                if (config.Layout == GridLayoutType.HexFlatTop)
                    index = HexGridUtils.HexToIndex(pos, gridSize);
                else
                    index = pos.x * gridSize.y + pos.y;
                    
                var cell = buffer[index];
                cell.IsHighlighted = true;
                buffer[index] = cell;

                EntityManager.SetComponentData(cell.CellEntity, new URPMaterialPropertyBaseColor { Value = colors.ColorYellow });

                _highlightedCells.Add(cell.CellEntity);
            }
        }

        offsets.Dispose();
    }

    private void HighlightMovementRange(DynamicBuffer<GridCellElement> buffer, int2 gridSize, int2 anchorCenter, int range, int2 unitSize, Entity ignoreUnit, UnitLayer layer, GridColorConfig colors)
    {
        var config = SystemAPI.GetSingleton<GridConfig>();

        if (config.Layout == GridLayoutType.HexFlatTop)
        {
            HighlightMovementRange_Hex(buffer, gridSize, anchorCenter, range, ignoreUnit, layer, colors);
        }
        else
        {
            HighlightMovementRange_Quad(buffer, gridSize, anchorCenter, range, unitSize, ignoreUnit, layer, colors);
        }
    }


    /// <summary>
    /// Hex движение (кубический алгоритм расстояния)
    /// </summary>
    /// <summary>
    /// Hex движение с отображением препятствий
    /// </summary>
    private void HighlightMovementRange_Hex(DynamicBuffer<GridCellElement> buffer, int2 gridSize, int2 center, int range, Entity ignoreUnit, UnitLayer layer, GridColorConfig colors)
    {
        NativeList<int2> reachableHexes = new NativeList<int2>(Allocator.Temp);
        HexGridUtils.GetHexesInRange(center, range, gridSize, ref reachableHexes);

        foreach (var hexPos in reachableHexes)
        {
            int index = HexGridUtils.HexToIndex(hexPos, gridSize);
            if (index < 0 || index >= buffer.Length) continue;

            var cell = buffer[index];
            Entity occupant = GetOccupant(cell, layer);
            bool isOccupied = IsCellOccupied(cell, layer);

            // 🔥 КЛЮЧЕВОЕ ОТЛИЧИЕ: красим ВСЕ клетки (и занятые, и свободные)
            cell.IsHighlighted = true;
            buffer[index] = cell;

            // Определяем цвет
            float4 color;
            if (isOccupied && occupant != ignoreUnit)
            {
                color = colors.ColorBlack; // 🔥 ЧЕРНЫЙ для препятствий
            }
            else
            {
                color = colors.ColorBlue; // Синий для свободных
            }

            EntityManager.SetComponentData(cell.CellEntity, new URPMaterialPropertyBaseColor { Value = color });
            _highlightedCells.Add(cell.CellEntity);
        }

        reachableHexes.Dispose();
    }


    /// <summary>
    /// Quad логика (оригинал)
    /// </summary>
    private void HighlightMovementRange_Quad(DynamicBuffer<GridCellElement> buffer, int2 gridSize, int2 anchorCenter, int range, int2 unitSize, Entity ignoreUnit, UnitLayer layer, GridColorConfig colors)
    {
        int rangeScore = range * 10;
        int startX = math.max(0, anchorCenter.x - range);
        int endX = math.min(gridSize.x - 1, anchorCenter.x + range);
        int startY = math.max(0, anchorCenter.y - range);
        int endY = math.min(gridSize.y - 1, anchorCenter.y + range);

        for (int ax = startX; ax <= endX; ax++)
        {
            for (int ay = startY; ay <= endY; ay++)
            {
                if (GetDistance(anchorCenter, new int2(ax, ay)) > rangeScore) continue;

                // 🔥 УБРАЛИ проверку CanUnitFitAt - красим ВСЕ клетки в радиусе

                for (int x = 0; x < unitSize.x; x++)
                {
                    for (int y = 0; y < unitSize.y; y++)
                    {
                        int tx = ax + x;
                        int ty = ay - y;
                        if (tx >= 0 && tx < gridSize.x && ty >= 0 && ty < gridSize.y)
                        {
                            int index = tx * gridSize.y + ty;
                            var cell = buffer[index];
                            cell.IsHighlighted = true;
                            buffer[index] = cell;

                            // 🔥 Определяем цвет для КАЖДОЙ клетки
                            Entity occupant = GetOccupant(cell, layer);
                            bool isOccupied = IsCellOccupied(cell, layer);
                            float4 color;

                            if (isOccupied && occupant != ignoreUnit)
                            {
                                color = colors.ColorBlack; // Черный для препятствий
                            }
                            else
                            {
                                color = colors.ColorBlue; // Синий для свободных
                            }

                            EntityManager.SetComponentData(cell.CellEntity, new URPMaterialPropertyBaseColor { Value = color });
                            _highlightedCells.Add(cell.CellEntity);
                        }
                    }
                }
            }
        }
    }





    private bool IsCellOccupied(GridCellElement cell, UnitLayer layer)
    {
        switch (layer)
        {
            case UnitLayer.Underground: return cell.IsOccupiedUnderground;
            case UnitLayer.Ground: return cell.IsOccupiedGround;
            case UnitLayer.Sky: return cell.IsOccupiedSky;
            default: return false;
        }
    }

    private Entity GetOccupant(GridCellElement cell, UnitLayer layer)
    {
        switch (layer)
        {
            case UnitLayer.Underground: return cell.OccupantUnderground;
            case UnitLayer.Ground: return cell.OccupantGround;
            case UnitLayer.Sky: return cell.OccupantSky;
            default: return Entity.Null;
        }
    }

    private bool CanUnitFitAt(int ax, int ay, int2 size, int2 gridSize, DynamicBuffer<GridCellElement> buffer, Entity ignoreEntity, UnitLayer layer)
    {
        for (int x = 0; x < size.x; x++)
        {
            for (int y = 0; y < size.y; y++)
            {
                int tx = ax + x;
                int ty = ay - y;

                if (tx < 0 || tx >= gridSize.x || ty < 0 || ty >= gridSize.y)
                    return false;

                int index = tx * gridSize.y + ty;
                var cell = buffer[index];

                if (IsCellOccupied(cell, layer) && GetOccupant(cell, layer) != ignoreEntity)
                    return false;
            }
        }
        return true;
    }

    private int GetDistance(int2 posA, int2 posB)
    {
        int dx = math.abs(posA.x - posB.x);
        int dy = math.abs(posA.y - posB.y);
        int minDelta = math.min(dx, dy);
        int maxDelta = math.max(dx, dy);
        return 14 * minDelta + 10 * (maxDelta - minDelta);
    }
}
