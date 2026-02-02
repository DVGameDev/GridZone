using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class GridSpawnerAuthoring : MonoBehaviour
{
    public enum GridLayoutMode
    {
        Quad = 0,
        HexFlatTop = 1
    }

    [Header("Grid Settings")]
    public GameObject QuadCellPrefab;
    public GameObject HexCellPrefab;

    public GridLayoutMode Layout = GridLayoutMode.Quad;

    public int Width = 10;
    public int Height = 10;
    public float Spacing = 1.1f;

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
    [Tooltip("Cell = стандартная подсветка клеток | Area = XCOM-style overlay")]
    public GridVisualMode VisualMode = GridVisualMode.Area;

    [Header("Grid Highlight Colors (RGBA)")]
    public Color ColorGray = new Color(0.5f, 0.5f, 0.5f, 0f);
    public Color ColorBlue = new Color(0f, 0f, 1f, 0.1f);
    public Color ColorYellow = new Color(1f, 0.92f, 0.016f, 0.1f);
    public Color ColorBlack = new Color(0f, 0f, 0f, 0.1f);
    public Color ColorGreen = new Color(0f, 1f, 0f, 0.1f);
    public Color ColorRed = new Color(1f, 0f, 0f, 0.1f);
    public Color ColorPurple = new Color(0.6f, 0f, 0.8f, 0.1f);

    public class Baker : Baker<GridSpawnerAuthoring>
    {
        public override void Bake(GridSpawnerAuthoring authoring)
        {
            // Выбираем prefab по режиму
            GameObject chosenPrefab =
                authoring.Layout == GridLayoutMode.Quad ? authoring.QuadCellPrefab : authoring.HexCellPrefab;

            if (chosenPrefab == null)
            {
                Debug.LogError($"[GridSpawnerAuthoring] Missing prefab for layout {authoring.Layout}.", authoring);
                return;
            }

            Entity prefabEntity = GetEntity(chosenPrefab, TransformUsageFlags.Dynamic);
            Entity entity = GetEntity(TransformUsageFlags.None);

            AddComponent(entity, new GridSpawnerComponent
            {
                PrefabEntity = prefabEntity,
                GridSize = new int2(authoring.Width, authoring.Height),
                Spacing = authoring.Spacing,
                InitialBrushSize = new int2(authoring.BrushSizeX, authoring.BrushSizeY),
                HeightSky = authoring.HeightSky,
                HeightGround = authoring.HeightGround,
                HeightUnderground = authoring.HeightUnderground,
                FacingMode = authoring.FacingMode,
                VisualMode = authoring.VisualMode,
                Layout = (GridLayoutType)authoring.Layout  // 🔥 ДОБАВЛЕНО
            });


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
        }
    }
}
