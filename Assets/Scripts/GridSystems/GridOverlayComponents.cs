using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Режим визуализации подсветки грида
/// </summary>
public enum GridVisualMode : byte
{
    Cell = 0,  // Стандартная подсветка через цвет материала клеток
    Area = 1   // XCOM-style: Mesh Overlay + Decal границы
}

/// <summary>
/// Запрос на обновление Area overlay
/// </summary>
public struct AreaOverlayRequest : IComponentData
{
    public InteractionMode Mode; // Move или Effect
}

/// <summary>
/// Буфер highlighted клеток для генерации overlay
/// </summary>
public struct OverlayCell : IBufferElementData
{
    public int2 GridPos;
}

/// <summary>
/// Данные активного overlay (синглтон)
/// </summary>
public struct ActiveOverlayData : IComponentData
{
    public Entity MeshEntity;      // Entity с MeshRenderer
    public Entity DecalEntity;     // Entity с DecalProjector (пока не используется)
    public InteractionMode Mode;   // Текущий режим (для смены цвета)
    public int CellCount;          // Количество клеток
}

/// <summary>
/// Параметры анимации overlay
/// </summary>
public struct OverlayAnimationData : IComponentData
{
    public float PulsePhase;       // [0..1] фаза пульсации
    public float PulseSpeed;       // Hz (частота пульсации)
    public float PulseIntensity;   // Амплитуда альфы
}

/// <summary>
/// Тег для overlay entities (для cleanup)
/// </summary>
public struct AreaOverlayTag : IComponentData { }

/// <summary>
/// Managed компонент для связи с GameObject (Mesh)
/// </summary>
public class MeshRendererReference : IComponentData
{
    public GameObject GameObject;
    public MeshRenderer Renderer;
    public Material Material;
}

/// <summary>
/// Managed компонент для связи с GameObject (Decal) - для будущего использования
/// </summary>
public class DecalProjectorReference : IComponentData
{
    public GameObject GameObject;
    public UnityEngine.Rendering.Universal.DecalProjector Projector;
}
public class LineRendererReference : IComponentData
{
    public GameObject GameObject;
    public LineRenderer Renderer;
}
