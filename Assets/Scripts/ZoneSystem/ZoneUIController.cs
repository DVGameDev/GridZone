using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UIElements;

public class FlowerHexagonController : MonoBehaviour
{
    public UIDocument uiDocument;

    private VisualElement[] _leftHexes = new VisualElement[7];
    private Label[] _leftLabels = new Label[7];
    private VisualElement[] _rightHexes = new VisualElement[7];
    private Label[] _rightLabels = new Label[7];

    private EntityManager _entityManager;
    private EntityQuery _zoneQuery;
    private EntityQuery _heroQuery;
    private EntityQuery _mapQuery;
    private EntityQuery _gridConfigQuery;
    private EntityQuery _radiationConfigQuery;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        var root = uiDocument.rootVisualElement;

        // Кэшируем ссылки на элементы из UXML
        for (int i = 0; i < 7; i++)
        {
            _leftHexes[i] = root.Q<VisualElement>($"left-hex-{i}");
            _leftLabels[i] = root.Q<Label>($"left-label-{i}");
            _rightHexes[i] = root.Q<VisualElement>($"right-hex-{i}");
            _rightLabels[i] = root.Q<Label>($"right-label-{i}");
        }

        // Кэшируем ECS запросы один раз
        _zoneQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ZoneModeTag>());
        _heroQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridCoordinates>(), ComponentType.ReadOnly<UnitIdComponent>());
        _mapQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridMapTag>());
        _gridConfigQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridConfig>());
        _radiationConfigQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ZoneRadiationConfig>());
    }

    void Update()
    {
        if (_zoneQuery.IsEmpty || _heroQuery.IsEmpty || _mapQuery.IsEmpty || _gridConfigQuery.IsEmpty)
            return;

        UpdateRadiationFlower();
        UpdateInfoFlower();
    }

    // ─── Левый цветочек: радиация ─────────────────────────────────

    void UpdateRadiationFlower()
    {
        // Находим герой (UnitId == 0)
        var entities = _heroQuery.ToEntityArray(Allocator.Temp);
        Entity heroEntity = Entity.Null;
        int2 heroPos = default;

        for (int i = 0; i < entities.Length; i++)
        {
            if (_entityManager.GetComponentData<UnitIdComponent>(entities[i]).UnitId == 0)
            {
                heroEntity = entities[i];
                heroPos = _entityManager.GetComponentData<GridCoordinates>(entities[i]).Value;
                break;
            }
        }
        entities.Dispose();

        if (heroEntity == Entity.Null) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_entityManager.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var buf = _entityManager.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        if (_radiationConfigQuery.IsEmpty) return;
        var config = _radiationConfigQuery.GetSingleton<ZoneRadiationConfig>();
        var grid = _gridConfigQuery.GetSingleton<GridConfig>();

        // Порядок индексов цветочка:
        // 0 = центр, 6 = верх (ось вверх), 1..5 по часовой от правого верхнего
        int2[] offsets = new int2[]
        {
            new int2(0, 0),    // 0: центр
            new int2(1, 0),    // 1: E
            new int2(1, -1),   // 2: SE
            new int2(0, -1),   // 3: S
            new int2(-1, 0),   // 4: W
            new int2(-1, 1),   // 5: NW
            new int2(0, 1),    // 6: N (верх)
        };

        for (int i = 0; i < 7; i++)
        {
            int2 pos = heroPos + offsets[i];
            int rad = GetRadiationAt(pos, buf, grid.GridSize);

            _leftLabels[i].text = rad >= 0 ? rad.ToString() : "—";
            _leftHexes[i].style.backgroundColor = GetRadiationColor(rad, config);
        }
    }

    int GetRadiationAt(int2 pos, DynamicBuffer<ZoneCellRadiation> buffer, int2 gridSize)
    {
        if (!HexGridUtils.IsHexInBounds(pos, gridSize)) return -1;
        int index = HexGridUtils.HexToIndex(pos, gridSize);
        if (index < 0 || index >= buffer.Length) return -1;
        return buffer[index].RadiationLevel;
    }

    Color GetRadiationColor(int radiation, ZoneRadiationConfig cfg)
    {
        if (radiation < 0) return new Color(0.3f, 0.3f, 0.3f, 0.5f); // вне карты

        Color c;
        if (radiation <= cfg.LevelGreen) c = new Color(cfg.ColorGreen.x, cfg.ColorGreen.y, cfg.ColorGreen.z);
        else if (radiation <= cfg.LevelYellow) c = new Color(cfg.ColorYellow.x, cfg.ColorYellow.y, cfg.ColorYellow.z);
        else if (radiation <= cfg.LevelOrange) c = new Color(cfg.ColorOrange.x, cfg.ColorOrange.y, cfg.ColorOrange.z);
        else c = new Color(cfg.ColorRed.x, cfg.ColorRed.y, cfg.ColorRed.z);
        c.a = 0.8f;
        return c;
    }

    // ─── Правый цветочек: пока просто серый ───────────────────────

    void UpdateInfoFlower()
    {
        for (int i = 0; i < 7; i++)
        {
            _rightLabels[i].text = "?";
            _rightHexes[i].style.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 0.4f);
        }
    }
}