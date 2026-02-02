using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Тег: режим ZONE активен (синглтон)
/// </summary>
public struct ZoneModeTag : IComponentData { }

/// <summary>
/// Конфиг радиации (синглтон)
/// </summary>
public struct ZoneRadiationConfig : IComponentData
{
    public int LevelGreen;   // 0
    public int LevelYellow;  // 5
    public int LevelOrange;  // 10
    public int LevelRed;     // 15

    public float4 ColorGreen;
    public float4 ColorYellow;
    public float4 ColorOrange;
    public float4 ColorRed;
}

/// <summary>
/// Конфиг генерации островов (синглтон)
/// </summary>
public struct ZoneIslandConfig : IComponentData
{
    // Green острова
    public float GreenProbability;
    public int GreenSizeMin;
    public int GreenSizeMax;

    // Orange острова
    public float OrangeProbability;
    public int OrangeSizeMin;
    public int OrangeSizeMax;

    // Red острова
    public float RedProbability;
    public int RedSizeMin;
    public int RedSizeMax;
}

/// <summary>
/// Радиация клетки (отдельный буфер на GridMap)
/// </summary>
public struct ZoneCellRadiation : IBufferElementData
{
    public int2 GridPos;
    public int RadiationLevel;   // 0, 5, 10, 15
    public bool IsVisited;       // Посещена героем
}

/// <summary>
/// Радиация героя (на entity юнита)
/// </summary>
public struct HeroRadiationData : IComponentData
{
    public int TotalRadiation;
}

/// <summary>
/// Компонент спавнера ZONE (удаляется после генерации)
/// </summary>
public struct ZoneSpawnerComponent : IComponentData
{
    public Entity HexCellPrefab;
    public int2 GridSize;
    public float HexSize;
}
