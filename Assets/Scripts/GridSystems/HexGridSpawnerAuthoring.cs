using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class HexGridSpawnerAuthoring : MonoBehaviour
{
    [Header("Hex Grid Settings")]
    public GameObject CellPrefab;
    public int Width = 10;   // q count
    public int Height = 10;  // r count

    [Tooltip("Hex size (radius).")]
    public float HexSize = 1.0f;

    [Header("Heights")]
    public float HeightSky = 3.0f;
    public float HeightGround = 0.0f;
    public float HeightUnderground = -3.0f;

    [Header("Brush Settings")]
    public int BrushSizeX = 1;
    public int BrushSizeY = 1;

    [Header("Game Rules")]
    public UnitFacingMode FacingMode = UnitFacingMode.Free;

    [Header("Grid Visual Style")]
    public GridVisualMode VisualMode = GridVisualMode.Area;

    public class Baker : Baker<HexGridSpawnerAuthoring>
    {
        public override void Bake(HexGridSpawnerAuthoring authoring)
        {
            if (authoring.CellPrefab == null) return;

            Entity prefabEntity = GetEntity(authoring.CellPrefab, TransformUsageFlags.Dynamic);
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new HexGridSpawnerComponent
            {
                PrefabEntity = prefabEntity,
                GridSize = new int2(authoring.Width, authoring.Height),
                HexSize = authoring.HexSize,
                InitialBrushSize = new int2(authoring.BrushSizeX, authoring.BrushSizeY),
                HeightSky = authoring.HeightSky,
                HeightGround = authoring.HeightGround,
                HeightUnderground = authoring.HeightUnderground,
                FacingMode = authoring.FacingMode,
                VisualMode = authoring.VisualMode
            });

            // Цвета берём из твоего существующего GridSpawnerAuthoring (если он есть) — чтобы не дублировать.
            // Тут намеренно НЕ добавляем GridColorConfig, чтобы не получить 2 синглтона.
        }
    }
}

