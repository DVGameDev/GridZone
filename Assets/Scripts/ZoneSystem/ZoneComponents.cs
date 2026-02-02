using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Базовый серый цвет грида (прозрачный)
/// </summary>
public struct ZoneBaseGridColor : IComponentData
{
    public float4 Color;
}

public struct ZoneCellRadiation : IBufferElementData
{
    public int2 GridPos;
    public Entity CellEntity; // 🔥 ДОБАВИТЬ
    public int RadiationLevel;
    public bool IsVisited;
}


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

/// <summary>
/// Конфиг генерации событий на карте
/// </summary>
public struct ZoneEventConfig : IComponentData
{
    // Вероятности генерации (0-1)
    public float AnomalyProbability;
    public float FightProbability;
    public float EventProbability;
}

/// <summary>
/// Тип события на карте
/// </summary>
public enum ZoneEventType : byte
{
    None = 0,
    Anomaly = 1,   // Аномалия
    Fight = 2,     // Драка
    Event = 3      // Ивент
}

/// <summary>
/// Событие на клетке карты
/// </summary>
public struct ZoneEventData : IComponentData
{
    public ZoneEventType EventType;
    public int Visibility;      // 0-3: на каком расстоянии обнаруживается
    public bool IsDiscovered;   // Обнаружено ли героем
    public int2 GridPos;        // Позиция на карте
}

/// <summary>
/// Буфер всех событий на карте (на GridMap entity)
/// </summary>
public struct ZoneEventElement : IBufferElementData
{
    public Entity EventEntity;
    public int2 GridPos;
    public ZoneEventType EventType;
    public int Visibility;
    public bool IsDiscovered;
}
