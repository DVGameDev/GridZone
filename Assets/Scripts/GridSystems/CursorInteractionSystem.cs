using Unity.Collections;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using System;

public static class HexGridRuntimeCache
{
    public static int2 GridSize;
}

/// <summary>
/// Система обработки взаимодействия курсора с сеткой:
/// - Превью курсора (цвета клеток)
/// - Обработка кликов (движение, поворот, эффекты)
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(GridHighlightSystem))]
public partial class CursorInteractionSystem : SystemBase
{
    private InputSystem_Actions _inputActions;
    private NativeList<Entity> _previousHoveredEntities;

    protected override void OnCreate()
    {
        _inputActions = new InputSystem_Actions();
        _inputActions.Game.Enable();
        _previousHoveredEntities = new NativeList<Entity>(Allocator.Persistent);

        RequireForUpdate<ActiveUnitComponent>();
        RequireForUpdate<GridConfig>();
        RequireForUpdate<GridMapTag>();
        RequireForUpdate<GridColorConfig>();
    }

    protected override void OnDestroy()
    {
        _inputActions.Game.Disable();
        _inputActions.Dispose();
        if (_previousHoveredEntities.IsCreated) _previousHoveredEntities.Dispose();
    }

    protected override void OnUpdate()
    {

        var config = SystemAPI.GetSingleton<GridConfig>();
        HexGridRuntimeCache.GridSize = config.GridSize;

        // 1. Подготовка контекста
        var context = PrepareContext();
        if (!context.IsValid) return;

        // 2. Raycast для определения клетки под курсором
        if (!TryRaycastCell(out int2 hitCoords))
        {
            // Если нет попадания - просто очищаем предыдущие цвета
            var ecbClear = new EntityCommandBuffer(Allocator.Temp);
            RestorePreviousColors(context, ecbClear);
            ecbClear.Playback(EntityManager);
            ecbClear.Dispose();
            return;
        }

        // 3. Генерация формы курсора
        var cursorData = GenerateCursorShape(context, hitCoords);

        // 4. Получаем буфер эффектов ЗАРАНЕЕ (до любых Playback)
        DynamicBuffer<EffectCommandBuffer> effectBuffer = default;
        if (SystemAPI.TryGetSingletonEntity<EffectCommandBufferTag>(out Entity bufferEntity))
        {
            // 🔥 БЕЗОПАСНАЯ ПРОВЕРКА: есть ли буфер на entity
            if (EntityManager.HasBuffer<EffectCommandBuffer>(bufferEntity))
            {
                effectBuffer = EntityManager.GetBuffer<EffectCommandBuffer>(bufferEntity);
            }
            else
            {
                Debug.LogWarning("[CursorInteractionSystem] EffectCommandBufferTag entity exists but has no buffer! Adding it now...");
                effectBuffer = EntityManager.AddBuffer<EffectCommandBuffer>(bufferEntity);
            }
        }
        else
        {
            // Если синглтона нет вообще - создаем на лету
            Debug.LogWarning("[CursorInteractionSystem] EffectCommandBuffer singleton not found! Creating fallback...");
            var fallbackEntity = EntityManager.CreateEntity();
            EntityManager.AddComponent<EffectCommandBufferTag>(fallbackEntity);
            effectBuffer = EntityManager.AddBuffer<EffectCommandBuffer>(fallbackEntity);
        }

        // 5. Обработка кликов (может изменять структуру)
        ProcessInput(context, cursorData, hitCoords, effectBuffer);

        // 6. EntityCommandBuffer для батчинга визуальных операций
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 7. Восстановление цветов предыдущих клеток
        RestorePreviousColors(context, ecb);

        // 8. Отрисовка превью курсора
        DrawCursorPreview(context, cursorData, hitCoords, ecb);

        // 9. Применяем все визуальные изменения батчем
        ecb.Playback(EntityManager);
        ecb.Dispose();

        cursorData.Offsets.Dispose();
    }

    #region Context Preparation

    private struct InteractionContext
    {
        public bool IsValid;
        public Entity SelectedUnit;
        public InteractionMode Mode;
        public UnitLayer Layer;
        public int2 GridSize;
        public GridConfig Config;
        public DynamicBuffer<GridCellElement> MapBuffer;
        public GridColorConfig Colors;
        public bool IsZoneMode;

    }

    private InteractionContext PrepareContext()
    {
        var context = new InteractionContext { IsValid = false };

        if (Camera.main == null) return context;

        var selectionState = SystemAPI.GetSingleton<ActiveUnitComponent>();
        context.SelectedUnit = selectionState.Unit;
        context.Mode = selectionState.Mode;
        context.IsZoneMode = SystemAPI.HasSingleton<ZoneModeTag>();


        if (context.SelectedUnit == Entity.Null || !EntityManager.Exists(context.SelectedUnit))
            return context;

        context.Config = SystemAPI.GetSingleton<GridConfig>();
        context.GridSize = context.Config.GridSize;
        context.Colors = SystemAPI.GetSingleton<GridColorConfig>();

        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        context.MapBuffer = EntityManager.GetBuffer<GridCellElement>(mapEntity);

        context.Layer = UnitLayer.Ground;
        if (EntityManager.HasComponent<UnitLayerData>(context.SelectedUnit))
            context.Layer = EntityManager.GetComponentData<UnitLayerData>(context.SelectedUnit).Value;

        context.IsValid = true;
        return context;
    }

    #endregion

    #region Color Restoration

    private void RestorePreviousColors(InteractionContext context, EntityCommandBuffer ecb)
    {
        foreach (var entity in _previousHoveredEntities)
        {
            if (EntityManager.Exists(entity))
            {
                // 🔥 Проверяем - подсвечена ли клетка move area
                var coords = EntityManager.GetComponentData<GridCoordinates>(entity);

                // 🔥 Универсальная индексация
                int index;
                if (context.Config.Layout == GridLayoutType.HexFlatTop)
                {
                    index = HexGridUtils.HexToIndex(coords.Value, context.GridSize);
                }
                else
                {
                    index = coords.Value.x * context.GridSize.y + coords.Value.y;
                }


                float4 restoreColor;

                if (index >= 0 && index < context.MapBuffer.Length)
                {
                    var cell = context.MapBuffer[index];

                    if (cell.IsHighlighted)
                    {
                        // Клетка подсвечена - восстанавливаем цвет подсветки (синий/черный)
                        bool isOccupied = false;
                        switch (context.Layer)
                        {
                            case UnitLayer.Underground: isOccupied = cell.IsOccupiedUnderground; break;
                            case UnitLayer.Ground: isOccupied = cell.IsOccupiedGround; break;
                            case UnitLayer.Sky: isOccupied = cell.IsOccupiedSky; break;
                        }

                        Entity occupant = Entity.Null;
                        switch (context.Layer)
                        {
                            case UnitLayer.Underground: occupant = cell.OccupantUnderground; break;
                            case UnitLayer.Ground: occupant = cell.OccupantGround; break;
                            case UnitLayer.Sky: occupant = cell.OccupantSky; break;
                        }

                        if (isOccupied && occupant != context.SelectedUnit)
                            restoreColor = context.Colors.ColorBlack;
                        else
                            restoreColor = context.Colors.ColorBlue;
                    }
                    else
                    {
                        // Клетка НЕ подсвечена - восстанавливаем базовый цвет
                        restoreColor = GetBaseColorForCell(entity, context);
                    }
                }
                else
                {
                    restoreColor = GetBaseColorForCell(entity, context);
                }

                ecb.SetComponent(entity, new URPMaterialPropertyBaseColor { Value = restoreColor });
            }
        }
        _previousHoveredEntities.Clear();
    }


    private float4 GetBaseColorForCell(Entity cell, InteractionContext context)
    {
        if (!EntityManager.HasComponent<GridCoordinates>(cell))
            return context.Colors.ColorGray;

        int2 c = EntityManager.GetComponentData<GridCoordinates>(cell).Value;

        int index;
        if (context.Config.Layout == GridLayoutType.HexFlatTop)
            index = HexGridUtils.HexToIndex(c, context.GridSize);
        else
            index = c.x * context.GridSize.y + c.y;

        if (index >= context.MapBuffer.Length)
            return context.Colors.ColorGray;

        var data = context.MapBuffer[index];

        // 🔥 ДЛЯ MOVE-РЕЖИМА: при восстановлении базового цвета
        if (context.Mode == InteractionMode.Move)
        {
            // если клетка занята – черный
            if (GridUtils.IsCellOccupied(data, context.Layer))
                return context.Colors.ColorBlack;

            // если клетка подсвечена (входит в move area) – синий
            if (data.IsHighlighted)
                return context.Colors.ColorBlue;

            // иначе базовый (серый или кастомный)
            if (EntityManager.HasComponent<CellCustomColor>(cell))
                return EntityManager.GetComponentData<CellCustomColor>(cell).BaseColor;

            return context.Colors.ColorGray;
        }

        // EFFECT и остальные как раньше, только с кастомным цветом
        if (data.IsHighlighted)
        {
            if (context.Mode == InteractionMode.Effect)
                return context.Colors.ColorYellow;
        }

        if (EntityManager.HasComponent<CellCustomColor>(cell))
            return EntityManager.GetComponentData<CellCustomColor>(cell).BaseColor;

        return context.Colors.ColorGray;
    }





    #endregion

    #region Raycast

    private bool TryRaycastCell(out int2 hitCoords)
    {
        hitCoords = new int2(-1, -1);

        if (Camera.main == null) return false;

        Vector2 mousePos = _inputActions.Game.Position.ReadValue<Vector2>();
        UnityEngine.Ray unityRay = Camera.main.ScreenPointToRay(mousePos);

        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
        var rayInput = new RaycastInput
        {
            Start = unityRay.origin,
            End = unityRay.origin + unityRay.direction * 1000f,
            Filter = CollisionFilter.Default
        };

        if (physicsWorld.CastRay(rayInput, out var hit))
        {
            if (EntityManager.HasComponent<GridCoordinates>(hit.Entity))
            {
                // 🔥 УНИВЕРСАЛЬНО: берем координаты напрямую из компонента
                hitCoords = EntityManager.GetComponentData<GridCoordinates>(hit.Entity).Value;
                return true;
            }
        }

        return false;
    }


    #endregion

    #region Cursor Shape Generation

    private struct CursorData
    {
        public NativeList<int2> Offsets;
        public bool IsValidAnchor;
        public bool IsBlocked;
        public Entity EffectEntity;
        public EffectStatComponent EffectStats;
    }

    private CursorData GenerateCursorShape(InteractionContext context, int2 hitCoords)
    {
        var data = new CursorData
        {
            Offsets = new NativeList<int2>(Allocator.Temp),
            IsValidAnchor = true,
            IsBlocked = false,
            EffectEntity = Entity.Null
        };

        if (context.Mode == InteractionMode.Move)
        {
            GenerateMoveCursorShape(context, hitCoords, ref data);
        }
        else if (context.Mode == InteractionMode.Effect)
        {
            GenerateEffectCursorShape(context, hitCoords, ref data);
        }

        return data;
    }

    private void GenerateMoveCursorShape(InteractionContext context, int2 hitCoords, ref CursorData data)
    {
        int2 unitSize = GridUtils.GetCurrentUnitSize(EntityManager, context.SelectedUnit);
        int2 unitPos = EntityManager.GetComponentData<GridCoordinates>(context.SelectedUnit).Value;
        int2 unitFacing = EntityManager.GetComponentData<UnitFacing>(context.SelectedUnit).Value;

        // Предсказываем Size курсора в зависимости от направления движения
        int2 cursorSize = GridUtils.PredictSizeForDirection(unitSize, unitFacing, unitPos, hitCoords, context.Config.FacingMode);

        for (int x = 0; x < cursorSize.x; x++)
            for (int y = 0; y < cursorSize.y; y++)
                data.Offsets.Add(new int2(x, -y));

        // Проверяем блокировку
        data.IsBlocked = UnitActionHelper.IsCursorBlocked(
     context.SelectedUnit,
     hitCoords,
     data.Offsets,  // 🔥 data вместо cursorData
     context.MapBuffer,
     context.GridSize,
     context.Layer,
     context.Config
 );


    }

    private void GenerateEffectCursorShape(InteractionContext context, int2 hitCoords, ref CursorData data)
    {
        if (!EntityManager.HasComponent<UnitEffectData>(context.SelectedUnit))
            return;

        var unitEffectData = EntityManager.GetComponentData<UnitEffectData>(context.SelectedUnit);
        if (!EntityManager.Exists(unitEffectData.EffectEntity) ||
            !EntityManager.HasComponent<EffectShapeData>(unitEffectData.EffectEntity))
            return;

        data.EffectEntity = unitEffectData.EffectEntity;
        if (EntityManager.HasComponent<EffectStatComponent>(data.EffectEntity))
            data.EffectStats = EntityManager.GetComponentData<EffectStatComponent>(data.EffectEntity);

        var shapeData = EntityManager.GetComponentData<EffectShapeData>(unitEffectData.EffectEntity);
        int2 unitPos = EntityManager.GetComponentData<GridCoordinates>(context.SelectedUnit).Value;
        int2 unitSize = GridUtils.GetCurrentUnitSize(EntityManager, context.SelectedUnit);
        int2 unitFacing = EntityManager.GetComponentData<UnitFacing>(context.SelectedUnit).Value;

        // Генерация формы в зависимости от типа прицеливания
        if (shapeData.AimShape.Type == AimShapeType.UnitPoint)
        {
            if (shapeData.EffectShape.Type == EffectShapeType.Cross)
                GridShapeHelper.GetOffsets_BodyCross(unitSize, shapeData.EffectShape.SizeX, ref data.Offsets);
            else
                GridShapeHelper.GetOffsets_BodySquareAura(unitSize, shapeData.EffectShape.SizeX, 0, ref data.Offsets);

            ShiftOffsetsToUnit(unitPos, hitCoords, ref data.Offsets);
        }
        else if (shapeData.AimShape.Type == AimShapeType.FacePoint)
        {
            GridShapeHelper.GetOffsets_FaceProjection(unitSize, unitFacing, shapeData.EffectShape.Type,
                shapeData.EffectShape.SizeX, shapeData.EffectShape.SizeZ, 0, ref data.Offsets);
            ShiftOffsetsToUnit(unitPos, hitCoords, ref data.Offsets);
        }
        else
        {
            EffectShapeConfig effectiveShape = shapeData.EffectShape;
            if (shapeData.AimShape.Type == AimShapeType.Ring || shapeData.AimShape.Type == AimShapeType.HalfRing)
            {
                effectiveShape.Type = EffectShapeType.Cell;
                effectiveShape.SizeX = 1;
            }
            GridShapeHelper.GetEffectOffsets_CursorBased(effectiveShape, unitFacing, ref data.Offsets);
        }

        // Валидация якоря
        // 🔥 ИСПРАВЛЕНО: добавлена проверка layout
        int anchorIdx;
        if (context.Config.Layout == GridLayoutType.HexFlatTop)
            anchorIdx = HexGridUtils.HexToIndex(hitCoords, context.GridSize);
        else
            anchorIdx = hitCoords.x * context.GridSize.y + hitCoords.y;
            
        if (anchorIdx < 0 || anchorIdx >= context.MapBuffer.Length || !context.MapBuffer[anchorIdx].IsHighlighted)
            data.IsValidAnchor = false;
    }

    private void ShiftOffsetsToUnit(int2 unitAnchor, int2 cursorHit, ref NativeList<int2> offsets)
    {
        NativeList<int2> shifted = new NativeList<int2>(Allocator.Temp);
        int2 baseShift = unitAnchor - cursorHit;
        foreach (var off in offsets.AsArray())
            shifted.Add(baseShift + off);

        offsets.Clear();
        offsets.AddRange(shifted.AsArray());
        shifted.Dispose();
    }

    #endregion

    #region Cursor Preview Drawing

    private void DrawCursorPreview(InteractionContext context, CursorData data, int2 hitCoords, EntityCommandBuffer ecb)
    {
        if (!data.IsValidAnchor) return;

        float4 targetColor = DetermineCursorColor(context, data);

        foreach (var offset in data.Offsets.AsArray())
        {
            int tx = hitCoords.x + offset.x;
            int ty = hitCoords.y + offset.y;
            if (tx < 0 || tx >= context.GridSize.x || ty < 0 || ty >= context.GridSize.y)
                continue;

            // 🔥 Универсальная индексация
            int index;
            if (context.Config.Layout == GridLayoutType.HexFlatTop)
            {
                index = HexGridUtils.HexToIndex(new int2(tx, ty), context.GridSize);
            }
            else
            {
                index = tx * context.GridSize.y + ty;
            }

            var cell = context.MapBuffer[index];

            _previousHoveredEntities.Add(cell.CellEntity);
            ecb.SetComponent(cell.CellEntity, new URPMaterialPropertyBaseColor { Value = targetColor });
        }
    }

    private float4 DetermineCursorColor(InteractionContext context, CursorData data)
    {
        if (context.Mode == InteractionMode.Move)
            return data.IsBlocked ? context.Colors.ColorRed : context.Colors.ColorGreen;

        if (context.Mode == InteractionMode.Effect)
            return context.Colors.ColorPurple;

        return context.Colors.ColorGray;
    }

    #endregion

    #region Input Processing

    private void ProcessInput(InteractionContext context, CursorData data, int2 hitCoords, DynamicBuffer<EffectCommandBuffer> effectBuffer)
    {
        bool isLeftClick = _inputActions.Game.Click.WasPerformedThisFrame();
        bool isShiftPressed = Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed;
        bool isMiddleClick = Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;

        bool isRotateClick = isMiddleClick || (isLeftClick && isShiftPressed);
        bool isActionClick = isLeftClick && !isShiftPressed && !isMiddleClick;

        if (!data.IsValidAnchor) return;

        if (context.Mode == InteractionMode.Move)
        {
            HandleMoveInput(context, data, hitCoords, isRotateClick, isActionClick);
        }
        else if (context.Mode == InteractionMode.Effect)
        {
            HandleEffectInput(context, data, hitCoords, isActionClick, effectBuffer);
        }
    }

    private void HandleMoveInput(InteractionContext context, CursorData data, int2 hitCoords, bool isRotateClick, bool isActionClick)
    {
        int2 unitPos = EntityManager.GetComponentData<GridCoordinates>(context.SelectedUnit).Value; 
        int2 currentSize = GridUtils.GetCurrentUnitSize(EntityManager, context.SelectedUnit);
        int2 currentFacing = EntityManager.GetComponentData<UnitFacing>(context.SelectedUnit).Value;

        // Поворот (Shift + Click)
        if (isRotateClick && context.Config.FacingMode != UnitFacingMode.Fixed)
        {
            UnitActionHelper.TryRotateUnit(
                EntityManager, context.SelectedUnit, unitPos, hitCoords,
                currentSize, currentFacing, context.Config.Spacing,
                context.MapBuffer, context.GridSize, context.Layer, context.Config);
        }
        // Движение (обычный клик)
        else if (isActionClick)
        {
            if (context.IsZoneMode)
            {
                // 🔥 ZONE MODE: можно ходить ТОЛЬКО на соседнюю клетку
                if (HexGridUtils.HexDistance(unitPos, hitCoords) <= 1)
                {
                    UnitActionHelper.TryMoveUnit(
                        EntityManager, context.SelectedUnit, unitPos, hitCoords,
                        currentSize, currentFacing, context.Config.Spacing,
                        context.MapBuffer, context.GridSize, context.Layer, context.Config);
                }
            }
            else if (!data.IsBlocked)
            {
                UnitActionHelper.TryMoveUnit(
                    EntityManager, context.SelectedUnit, unitPos, hitCoords,
                    currentSize, currentFacing, context.Config.Spacing,
                    context.MapBuffer, context.GridSize, context.Layer, context.Config);
            }
        }

    }

    private void HandleEffectInput(InteractionContext context, CursorData data, int2 hitCoords, bool isActionClick, DynamicBuffer<EffectCommandBuffer> commandBuffer)
    {
        if (data.EffectEntity == Entity.Null || !isActionClick)
            return;

        if (!commandBuffer.IsCreated)
        {
            Debug.LogError("[CursorInteractionSystem] EffectCommandBuffer not available!");
            return;
        }

        NativeHashSet<Entity> uniqueTargets = new NativeHashSet<Entity>(16, Allocator.Temp);

        foreach (var offset in data.Offsets.AsArray())
        {
            int tx = hitCoords.x + offset.x;
            int ty = hitCoords.y + offset.y;

            if (tx < 0 || tx >= context.GridSize.x || ty < 0 || ty >= context.GridSize.y)
                continue;

            // 🔥 Универсальная индексация
            int index;
            if (context.Config.Layout == GridLayoutType.HexFlatTop)
            {
                index = HexGridUtils.HexToIndex(new int2(tx, ty), context.GridSize);
            }
            else
            {
                index = tx * context.GridSize.y + ty;
            }

            var cell = context.MapBuffer[index];

            if (cell.IsOccupiedGround && cell.OccupantGround != Entity.Null)
                uniqueTargets.Add(cell.OccupantGround);
            if (cell.IsOccupiedSky && cell.OccupantSky != Entity.Null)
                uniqueTargets.Add(cell.OccupantSky);
            if (cell.IsOccupiedUnderground && cell.OccupantUnderground != Entity.Null)
                uniqueTargets.Add(cell.OccupantUnderground);
        }

        EffectType parsedType = EffectType.None;
        Enum.TryParse(data.EffectStats.Name.ToString(), out parsedType);

        foreach (var target in uniqueTargets)
        {
            commandBuffer.Add(new EffectCommandBuffer
            {
                Command = new EffectCommand
                {
                    Type = parsedType,
                    StatusType = data.EffectStats.StatusType,
                    SourceUnit = context.SelectedUnit,
                    TargetUnit = target,
                    TargetCard = Entity.Null,
                    Power = data.EffectStats.Power,
                    Repeat = data.EffectStats.Repeat,
                    Duration = data.EffectStats.Duration,
                    Charges = data.EffectStats.Charges,
                    Description = data.EffectStats.Description,
                    IsVisible = data.EffectStats.IsVisible
                }
            });
        }

        Debug.Log($"[CursorInteractionSystem] Added {uniqueTargets.Count} effect commands to buffer");
        uniqueTargets.Dispose();
    }

    #endregion
}
