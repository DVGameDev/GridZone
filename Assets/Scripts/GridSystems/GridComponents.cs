using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Тег: у клетки есть кастомный базовый цвет
/// УНИВЕРСАЛЬНАЯ система - можно использовать для любых раскрасок
/// </summary>
public struct CellCustomColor : IComponentData
{
    public float4 BaseColor; // Базовый цвет клетки (не серый)
}



// Компонент-событие для смены режима из UI
public struct UIActionRequest : IComponentData
{
    public InteractionMode RequestedMode;
    public bool IsPending;
}

// ===========================
// GRID CORE
// ===========================

public enum GridLayoutType : byte
{
    Quad = 0,
    HexFlatTop = 1
}

public struct GridMapTag : IComponentData
{
    public int2 Size;
}

public struct GridCellElement : IBufferElementData
{
    public Entity CellEntity;

    public bool IsOccupiedUnderground;
    public bool IsOccupiedGround;
    public bool IsOccupiedSky;

    public Entity OccupantUnderground;
    public Entity OccupantGround;
    public Entity OccupantSky;

    public bool IsHighlighted; // Подсвечена ли клетка (синим/желтым)
}

public struct GridCoordinates : IComponentData
{
    // Для Quad: (x,y). Для Hex: (q,r) в axial (flat-top)
    public int2 Value;
}

public struct GridConfig : IComponentData
{
    public int2 GridSize;
    public int2 BrushSize;

    /// <summary>
    /// Для Quad: шаг клетки по X/Z.
    /// Для Hex: size (радиус) хекса.
    /// </summary>
    public float Spacing;

    public float HeightSky;
    public float HeightGround;
    public float HeightUnderground;

    public UnitFacingMode FacingMode;
    public GridVisualMode VisualMode;

    public GridLayoutType Layout; // NEW
}

///
/// Конфигурация цветов для подсветки грида
///
public struct GridColorConfig : IComponentData
{
    public float4 ColorGray;   // Обычная клетка
    public float4 ColorBlue;   // Доступная для движения
    public float4 ColorYellow; // Зона эффекта (aim)
    public float4 ColorBlack;  // Препятствие
    public float4 ColorGreen;  // Курсор (можно переместить)
    public float4 ColorRed;    // Курсор (заблокирован)
    public float4 ColorPurple; // Курсор эффекта
}

public struct GridSpawnerComponent : IComponentData
{
    public Entity PrefabEntity;

    public int2 GridSize;
    public float Spacing;

    public int2 InitialBrushSize;

    public float HeightSky;
    public float HeightGround;
    public float HeightUnderground;

    public UnitFacingMode FacingMode;
    public GridVisualMode VisualMode;

    public GridLayoutType Layout; // NEW
}

// ===========================
// INTERACTION & SELECTION
// ===========================

public enum InteractionMode
{
    None,
    Move,
    Effect
}

// ===========================
// VALIDATION HELPERS
// ===========================

public struct CellValidationContext
{
    public Entity SelectedUnit;
    public UnitLayer ViewerLayer;
    public bool CheckOccupancy;
    public bool CheckHighlight;
}

public struct CursorMaskResult
{
    public bool IsInsideGrid;
    public bool IsAllHighlighted;
    public bool IsBlocked;
    public bool IsInRange;
}

// ===========================
// LEGACY / UTILITY
// ===========================

public struct ClickableComponent : IComponentData { }

public struct GridCellState : IComponentData
{
    public bool IsSelected;
}

public struct CellOccupied : IComponentData
{
    public bool Value;
}

public struct GridCellEntityElement : IBufferElementData
{
    public Entity CellEntity;
}

public struct UnitPrefabElement : IBufferElementData
{
    public FixedString64Bytes UnitName;
    public Entity PrefabEntity;
}

public struct HexGridSpawnerComponent : IComponentData
{
    public Entity PrefabEntity;
    public int2 GridSize;        // qCount, rCount
    public float HexSize;        // radius
    public int2 InitialBrushSize;

    public float HeightSky;
    public float HeightGround;
    public float HeightUnderground;

    public UnitFacingMode FacingMode;
    public GridVisualMode VisualMode;
}
