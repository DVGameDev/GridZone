using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Authoring для ZONE режима
/// Размещай этот компонент на GameObject в сцене для активации ZONE
/// </summary>
public class ZoneSpawnerAuthoring : MonoBehaviour
{
    [Header("=== ZONE MODE ===")]
    [Tooltip("Активирует ZONE режим вместо стандартного Grid")]
    public bool EnableZoneMode = true;

    [Header("Hex Grid Settings")]
    public GameObject HexCellPrefab;
    public int Width = 20;
    public int Height = 20;
    [Tooltip("Hex size (radius)")]
    public float HexSize = 1.0f;

    [Header("Radiation Levels (Values)")]
    public int LevelGreen = 0;
    public int LevelYellow = 5;
    public int LevelOrange = 10;
    public int LevelRed = 15;

   
    [Header("Radiation Colors (with Alpha for transparency)")]
    public Color ColorGreen = new Color(0, 1, 0, 0.3f);
    public Color ColorYellow = new Color(1, 0.92f, 0.016f, 0.3f);
    public Color ColorOrange = new Color(1, 0.5f, 0, 0.3f);
    public Color ColorRed = new Color(1, 0, 0, 0.3f);

    [Header("Island Generation Probabilities")]
    [Range(0f, 1f)] public float GreenProbability = 0.15f;
    public int GreenSizeMin = 2;
    public int GreenSizeMax = 5;

    [Range(0f, 1f)] public float OrangeProbability = 0.1f;
    public int OrangeSizeMin = 2;
    public int OrangeSizeMax = 5;

    [Range(0f, 1f)] public float RedProbability = 0.05f;
    public int RedSizeMin = 1;
    public int RedSizeMax = 3;

    [Header("Map Events Generation")]
    [Tooltip("Вероятность генерации аномалии на клетке")]
    [Range(0f, 1f)] public float AnomalyProbability = 0.05f;
    [Tooltip("Вероятность генерации драки на клетке")]
    [Range(0f, 1f)] public float FightProbability = 0.03f;
    [Tooltip("Вероятность генерации ивента на клетке")]
    [Range(0f, 1f)] public float EventProbability = 0.02f;

    [Header("Battery Settings")]
    [Tooltip("Максимальная ёмкость аккумулятора")]
    public float MaxBatteryCapacity = 100f;

    [Header("Grid Highlight Colors (для совместимости)")]
    [Tooltip("Эти цвета используются системами подсветки")]
    public Color ColorGray = new Color(0.5f, 0.5f, 0.5f, 0f);
    public Color ColorBlue = new Color(0f, 0f, 1f, 0.5f);
    //public Color ColorYellow = new Color(1f, 0.92f, 0.016f, 0.5f);
    public Color ColorBlack = new Color(0f, 0f, 0f, 0.8f);
    //public Color ColorGreen = new Color(0f, 1f, 0f, 0.5f);
    //public Color ColorRed = new Color(1f, 0f, 0f, 0.5f);
    public Color ColorPurple = new Color(0.6f, 0f, 0.8f, 0.5f);

    public class Baker : Baker<ZoneSpawnerAuthoring>
    {
        public override void Bake(ZoneSpawnerAuthoring authoring)
        {
            if (!authoring.EnableZoneMode || authoring.HexCellPrefab == null)
                return;

            var entity = GetEntity(TransformUsageFlags.None);

            // 1. Компонент спавнера
            AddComponent(entity, new ZoneSpawnerComponent
            {
                HexCellPrefab = GetEntity(authoring.HexCellPrefab, TransformUsageFlags.Dynamic),
                GridSize = new int2(authoring.Width, authoring.Height),
                HexSize = authoring.HexSize
            });

            // 2. Конфиг радиации
            AddComponent(entity, new ZoneRadiationConfig
            {
                LevelGreen = authoring.LevelGreen,
                LevelYellow = authoring.LevelYellow,
                LevelOrange = authoring.LevelOrange,
                LevelRed = authoring.LevelRed,

                ColorGreen = new float4(authoring.ColorGreen.r, authoring.ColorGreen.g, authoring.ColorGreen.b, authoring.ColorGreen.a),
                ColorYellow = new float4(authoring.ColorYellow.r, authoring.ColorYellow.g, authoring.ColorYellow.b, authoring.ColorYellow.a),
                ColorOrange = new float4(authoring.ColorOrange.r, authoring.ColorOrange.g, authoring.ColorOrange.b, authoring.ColorOrange.a),
                ColorRed = new float4(authoring.ColorRed.r, authoring.ColorRed.g, authoring.ColorRed.b, authoring.ColorRed.a)
            });

            // 3. Конфиг островов
            AddComponent(entity, new ZoneIslandConfig
            {
                GreenProbability = authoring.GreenProbability,
                GreenSizeMin = authoring.GreenSizeMin,
                GreenSizeMax = authoring.GreenSizeMax,

                OrangeProbability = authoring.OrangeProbability,
                OrangeSizeMin = authoring.OrangeSizeMin,
                OrangeSizeMax = authoring.OrangeSizeMax,

                RedProbability = authoring.RedProbability,
                RedSizeMin = authoring.RedSizeMin,
                RedSizeMax = authoring.RedSizeMax
            });

            // 3.5. Конфиг событий
            AddComponent(entity, new ZoneEventConfig
            {
                AnomalyProbability = authoring.AnomalyProbability,
                FightProbability = authoring.FightProbability,
                EventProbability = authoring.EventProbability
            });


            // 4. Тег режима
            AddComponent(entity, new ZoneModeTag());
            // 5. GridColorConfig (для совместимости с GridHighlightSystem)
            AddComponent(entity, new GridColorConfig
            {
                ColorGray = new float4(authoring.ColorGray.r, authoring.ColorGray.g, authoring.ColorGray.b, authoring.ColorGray.a),
                ColorBlue = new float4(authoring.ColorBlue.r, authoring.ColorBlue.g, authoring.ColorBlue.b, authoring.ColorBlue.a),
                ColorYellow = new float4(authoring.ColorYellow.r, authoring.ColorYellow.g, authoring.ColorYellow.b, authoring.ColorYellow.a),
                ColorBlack = new float4(authoring.ColorBlack.r, authoring.ColorBlack.g, authoring.ColorBlack.b, authoring.ColorBlack.a),
                ColorGreen = new float4(authoring.ColorGreen.r, authoring.ColorGreen.g, authoring.ColorGreen.b, authoring.ColorGreen.a),
                ColorRed = new float4(authoring.ColorRed.r, authoring.ColorRed.g, authoring.ColorRed.b, authoring.ColorRed.a),
                ColorPurple = new float4(authoring.ColorPurple.r, authoring.ColorPurple.g, authoring.ColorPurple.b, authoring.ColorPurple.a)
            });
            // 6. Базовый серый цвет грида
            AddComponent(entity, new ZoneBaseGridColor
            {
                Color = new float4(authoring.ColorGray.r, authoring.ColorGray.g, authoring.ColorGray.b, authoring.ColorGray.a)
            });

            // 7. Аккумулятор (синглтон для всей зоны)
            AddComponent(entity, new BatteryData
            {
                CurrentCharge = authoring.MaxBatteryCapacity, // Изначально полный
                MaxCharge = authoring.MaxBatteryCapacity
            });


        }
    }
}
