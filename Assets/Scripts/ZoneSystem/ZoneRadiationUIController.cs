using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Контроллер UI для отображения радиации вокруг героя
/// Левый цветочек - радиация вокруг героя
/// Правый цветочек - информация о событиях (будущее расширение)
/// </summary>
public class ZoneRadiationUIController : MonoBehaviour
{
    [Header("UI References")]
    public UIDocument uiDocument;
    
    [Header("Layout Settings")]
    public int LeftFlowerX = 50;
    public int LeftFlowerY = 50;
    public int RightFlowerX = -350;  // Отступ от правого края
    public int RightFlowerY = 50;

    private VisualElement _root;
    private VisualElement _leftFlower;
    private VisualElement _rightFlower;
    private EntityManager _entityManager;

    // Кэш для label и hex элементов
    private Label[] _leftLabels = new Label[7];
    private VisualElement[] _leftHexes = new VisualElement[7];
    private Label[] _rightLabels = new Label[7];
    private VisualElement[] _rightHexes = new VisualElement[7];

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _root = uiDocument.rootVisualElement;

        CreateFlowers();
    }

    void CreateFlowers()
    {
        // Создаём левый цветочек (радиация)
        _leftFlower = CreateFlowerContainer("left-flower");
        _leftFlower.style.left = LeftFlowerX;
        _leftFlower.style.top = LeftFlowerY;
        _root.Add(_leftFlower);

        for (int i = 0; i < 7; i++)
        {
            var hex = CreateHexagon($"left-hex-{i}", i);
            _leftFlower.Add(hex);
            _leftHexes[i] = hex;
            _leftLabels[i] = hex.Q<Label>();
        }

        // Создаём правый цветочек (события/инфо)
        _rightFlower = CreateFlowerContainer("right-flower");
        _rightFlower.style.right = -RightFlowerX;  // Используем right вместо left
        _rightFlower.style.top = RightFlowerY;
        _root.Add(_rightFlower);

        for (int i = 0; i < 7; i++)
        {
            var hex = CreateHexagon($"right-hex-{i}", i);
            _rightFlower.Add(hex);
            _rightHexes[i] = hex;
            _rightLabels[i] = hex.Q<Label>();
        }
    }

    VisualElement CreateFlowerContainer(string name)
    {
        var container = new VisualElement();
        container.name = name;
        container.style.position = Position.Absolute;
        container.style.width = 300;
        container.style.height = 300;
        return container;
    }

    VisualElement CreateHexagon(string name, int index)
    {
        var hex = new VisualElement();
        hex.name = name;
        hex.style.position = Position.Absolute;
        hex.style.width = 60;
        hex.style.height = 60;
        hex.style.backgroundColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);
        hex.style.borderTopLeftRadius = 10;
        hex.style.borderTopRightRadius = 10;
        hex.style.borderBottomLeftRadius = 10;
        hex.style.borderBottomRightRadius = 10;
        hex.style.alignItems = Align.Center;
        hex.style.justifyContent = Justify.Center;

        // Позиционирование
        var pos = GetHexPosition(index);
        hex.style.left = pos.x;
        hex.style.top = pos.y;

        // Label
        var label = new Label("?");
        label.style.color = Color.black;
        label.style.fontSize = 16;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        hex.Add(label);

        return hex;
    }

    Vector2 GetHexPosition(int index)
    {
        // Позиции как в оригинальном USS
        // hex-0 (центр) - индекс 0
        // hex-1..6 - вокруг по часовой стрелке, начиная с правого верхнего
        switch (index)
        {
            case 0: return new Vector2(120, 120); // Центр
            case 1: return new Vector2(180, 90);  // Правый верхний
            case 2: return new Vector2(180, 150); // Правый нижний
            case 3: return new Vector2(120, 180); // Нижний
            case 4: return new Vector2(60, 150);  // Левый нижний
            case 5: return new Vector2(60, 90);   // Левый верхний
            case 6: return new Vector2(120, 60);  // Верхний (ось вверх)
            default: return Vector2.zero;
        }
    }

    void Update()
    {
       //if (!_entityManager.Exists(_entityManager))
       //     return;

        // Обновляем левый цветочек (радиация)
        UpdateRadiationFlower();

        // Обновляем правый цветочек (пока пустой)
        UpdateInfoFlower();
    }

    void UpdateRadiationFlower()
    {
        // Проверяем ZONE режим
        var zoneQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ZoneModeTag>());
        if (zoneQuery.IsEmpty) return;

        // Находим героя
        var heroQuery = _entityManager.CreateEntityQuery(
            ComponentType.ReadOnly<GridCoordinates>(),
            ComponentType.ReadOnly<UnitIdComponent>());

        if (heroQuery.IsEmpty) return;

        var heroEntity = Entity.Null;
        int2 heroPos = new int2(-1, -1);

        foreach (var entity in heroQuery.ToEntityArray(Unity.Collections.Allocator.Temp))
        {
            var unitId = _entityManager.GetComponentData<UnitIdComponent>(entity);
            if (unitId.UnitId == 0)
            {
                heroEntity = entity;
                heroPos = _entityManager.GetComponentData<GridCoordinates>(entity).Value;
                break;
            }
        }

        if (heroEntity == Entity.Null) return;

        // Получаем карту радиации
        var mapQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridMapTag>());
        if (mapQuery.IsEmpty) return;

        var mapEntity = mapQuery.GetSingletonEntity();
        if (!_entityManager.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var radiationBuffer = _entityManager.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        var radiationConfig = zoneQuery.GetSingleton<ZoneRadiationConfig>();
        var gridConfig = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridConfig>()).GetSingleton<GridConfig>();

        // Получаем радиацию в центре и вокруг
        int[] radiationLevels = new int[7];
        radiationLevels[0] = GetRadiationAt(heroPos, radiationBuffer, gridConfig.GridSize);

        // Соседи в hex grid (порядок важен!)
        // Индекс 6 - это верх (ось вверх), остальные по часовой
        int2[] neighborOffsets = new int2[]
        {
            new int2(0, 1),   // hex-6: Верх (NE)
            new int2(1, 0),   // hex-1: Правый верхний (E)
            new int2(1, -1),  // hex-2: Правый нижний (SE)
            new int2(0, -1),  // hex-3: Низ (SW)
            new int2(-1, 0),  // hex-4: Левый нижний (W)
            new int2(-1, 1),  // hex-5: Левый верхний (NW)
        };

        for (int i = 0; i < 6; i++)
        {
            int2 neighborPos = heroPos + neighborOffsets[i];
            int hexIndex = (i == 0) ? 6 : i;  // Первый offset идёт в hex-6
            radiationLevels[hexIndex] = GetRadiationAt(neighborPos, radiationBuffer, gridConfig.GridSize);
        }

        // Обновляем UI
        for (int i = 0; i < 7; i++)
        {
            int radiation = radiationLevels[i];
            _leftLabels[i].text = radiation.ToString();
            _leftHexes[i].style.backgroundColor = GetRadiationColor(radiation, radiationConfig);
        }
    }

    void UpdateInfoFlower()
    {
        // Пока оставляем серыми с вопросами
        for (int i = 0; i < 7; i++)
        {
            _rightLabels[i].text = "?";
            _rightHexes[i].style.backgroundColor = new Color(0.6f, 0.6f, 0.6f, 0.3f);
        }
    }

    int GetRadiationAt(int2 pos, DynamicBuffer<ZoneCellRadiation> buffer, int2 gridSize)
    {
        if (!HexGridUtils.IsHexInBounds(pos, gridSize))
            return -1;

        int index = HexGridUtils.HexToIndex(pos, gridSize);
        if (index < 0 || index >= buffer.Length)
            return -1;

        return buffer[index].RadiationLevel;
    }

    Color GetRadiationColor(int radiation, ZoneRadiationConfig config)
    {
        if (radiation < 0)
            return new Color(0.3f, 0.3f, 0.3f, 0.5f); // Вне карты

        Color color;
        if (radiation == config.LevelGreen)
            color = new Color(config.ColorGreen.x, config.ColorGreen.y, config.ColorGreen.z);
        else if (radiation == config.LevelYellow)
            color = new Color(config.ColorYellow.x, config.ColorYellow.y, config.ColorYellow.z);
        else if (radiation == config.LevelOrange)
            color = new Color(config.ColorOrange.x, config.ColorOrange.y, config.ColorOrange.z);
        else if (radiation == config.LevelRed)
            color = new Color(config.ColorRed.x, config.ColorRed.y, config.ColorRed.z);
        else
            color = Color.gray;

        color.a = 0.8f;
        return color;
    }
}
