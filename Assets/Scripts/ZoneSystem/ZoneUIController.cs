using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

///
/// Контроллер двух цветочков.
/// Левый — радиация вокруг героя.
/// Правый — детектор аномалий с 3 режимами: Off / MultiCell / ArcMode.
///
public class ZoneUIController : MonoBehaviour
{
    public UIDocument uiDocument;
    VisualElement _radiationFill;
    Label _radiationLabel;
    Label _radiationValueLabel; // 🔥 НОВОЕ: цифра под полосой радиации

    // ── Аккумулятор ────────────────────────────────────────────────
    VisualElement _batteryFill;
    Label _batteryLabel;
    Label _batteryValueLabel; // 🔥 НОВОЕ: цифра под полосой аккумулятора

    float _cachedRadiation = -1f;

    // ── cached UI refs ────────────────────────────────────────────
    private VisualElement[] _leftHexes = new VisualElement[7];
    private Label[] _leftLabels = new Label[7];
    private VisualElement[] _rightHexes = new VisualElement[7];
    private Label[] _rightLabels = new Label[7];
    private Button _btnMode;
    private Button _btnPower;
    private Button _btnDebugRadiation;
    private Button _btnDebugEvents;

    // ── Кнопка режима дозиметра (левый цветочек) ──────────────────
    private Button _btnRadiationMode;

    // ── Кнопка визуальных приборов ─────────────────────────────────
    private Button _btnVisualDevice; // 🔥 НОВОЕ

    // ── Кнопка системы фильтрации ──────────────────────────────────
    private Button _btnFilterSystem; // 🔥 НОВОЕ

    // ── cached ECS queries ────────────────────────────────────────
    private EntityManager _em;
    private EntityQuery _moveQuery;
    private EntityQuery _radQuery;
    private EntityQuery _zoneQuery;
    private EntityQuery _heroQuery;
    private EntityQuery _mapQuery;
    private EntityQuery _gridConfigQuery;
    private EntityQuery _radiationConfigQuery;

    // ── детектор: режим и мощность ─────────────────────────────────
    private enum DetectorMode { Off, ArcMode, SingleCell, MultiCell }
    private DetectorMode _mode = DetectorMode.Off;
    private int _power = 1; // 1..6

    // ── Энергопотребление детектора ──────────────────────────────
    private static readonly float[] ModePowerCost = new float[]
    {
        0f,  // Off - не потребляет
        3f,  // MultiCell - 
        1f,  // SingleCell - 
        3f   // ArcMode - 
    };
    // 🔥 НОВОЕ: Стоимость уровня мощности детектора
    private static readonly float[] PowerLevelCost = new float[]
    {
    1f,  // Power 1 - базовый, бесплатный
    2f,  // Power 2 - +1 энергия
    3f,  // Power 3 - +2 энергии
    4f,  // Power 4 - +3 энергии
    5f,  // Power 5 - +4 энергии
    6f   // Power 6 - +5 энергий
    };
    // ── дозиметр: режим ─────────────────────────────────────────────
    private enum RadiationMode { Off, ArcRad, PowerCell, SingleCell, MultiCell }
    private RadiationMode _radiationMode = RadiationMode.Off;

    // ── Энергопотребление дозиметра ──────────────────────────────
    private static readonly float[] RadiationModeCost = new float[]
    {
        0f,  // Off - не потребляет
        6f,  // MultiCell - 
        4f,  // PowerCell - 
        2f,  // SingleCell -
        3f   // ArcRad - 
    };

    // ── визуальные приборы ──────────────────────────────────────────
    private enum VisualDeviceMode { Off, EVBinoculars, ThermoImager } // 🔥 НОВОЕ
    private VisualDeviceMode _visualDevice = VisualDeviceMode.Off; // 🔥 НОВОЕ

    // ── Энергопотребление визуальных приборов ────────────────────
    private static readonly float[] VisualDeviceCost = new float[]
    {
        0f,  // Off - не потребляет
        2f,  // EVBinoculars - 2 энергии
        6f   // ThermoImager - 4 энергии
    };

    // ── Бонус к обнаружению от визуальных приборов ────────────────
    private static readonly int[] VisualDeviceDetectionBonus = new int[]
    {
        0,  // Off - без бонуса
        1,  // EVBinoculars - +1 к обнаружению
        3   // ThermoImager - +3 к обнаружению
    };

    // ── система очистки фильтров ────────────────────────────────────
    private enum FilterMode { Off, StdFiltering, MaxFiltering, OverloadFiltering } // 🔥 НОВОЕ
    private FilterMode _filterMode = FilterMode.Off; // 🔥 НОВОЕ

    // Нейтрализация радиации за ход
    private static readonly int[] FilterRadReduction = new int[]
    {
        0,   // Off
        3,   // StdFiltering
        8,   // MaxFiltering
        12   // OverloadFiltering
    };

    // Энергопотребление за ход
    private static readonly float[] FilterEnergyCost = new float[]
    {
        0f,  // Off
        3f,  // StdFiltering
        10f, // MaxFiltering
        20f  // OverloadFiltering
    };

    // ── отслеживание движения для обновления ОДИН РАЗ ───────────────
    private bool _wasMovingLastFrame = false;
    private int2 _lastHeroPos = new int2(-9999, -9999);

    // ── флаги отладки ──────────────────────────────────────────────
    private bool _debugRadiationEnabled = false;
    private bool _debugEventsEnabled = false;

    // ── направления цветочка (индекс hex → axial offset) ──────────
    // 0=центр, 1=E, 2=SE, 3=S, 4=W, 5=NW, 6=N(верх)
    private static readonly int2[] HexOffsets = new int2[]
    {
        new int2( 0,  0), // 0 центр
        new int2( 1,  0), // 1 E
        new int2( 1, -1), // 2 SE
        new int2( 0, -1), // 3 S
        new int2(-1,  0), // 4 W
        new int2(-1,  1), // 5 NW
        new int2( 0,  1), // 6 N (верх)
    };

    // ── цвета ──────────────────────────────────────────────────────
    private static readonly Color ColorOff = new Color(0.15f, 0.15f, 0.15f, 0.6f);
    private static readonly Color ColorEmpty = new Color(0.22f, 0.22f, 0.30f, 0.7f);
    private static readonly Color ColorAnomalyFar = new Color(0.7f, 0.2f, 0.9f, 0.7f);
    private static readonly Color ColorAnomalyMid = new Color(0.9f, 0.5f, 0.1f, 0.8f);
    private static readonly Color ColorAnomalyNear = new Color(1.0f, 0.15f, 0.15f, 0.9f);
    private static readonly Color ColorArcHighlight = new Color(0.3f, 0.6f, 0.9f, 0.6f);

    private const int MAX_SCAN_RANGE = 50;

    void Start()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var root = uiDocument.rootVisualElement;

        // Кэшируем хексы
        for (int i = 0; i < 7; i++)
        {
            _leftHexes[i] = root.Q<VisualElement>($"left-hex-{i}");
            _leftLabels[i] = root.Q<Label>($"left-label-{i}");
            _rightHexes[i] = root.Q<VisualElement>($"right-hex-{i}");
            _rightLabels[i] = root.Q<Label>($"right-label-{i}");
        }

        _radiationFill = root.Q<VisualElement>("radiation-bar-fill");
        _radiationLabel = root.Q<Label>("radiation-bar-label");
        _radiationValueLabel = root.Q<Label>("radiation-value-label");

        // Аккумулятор
        _batteryFill = root.Q<VisualElement>("battery-bar-fill");
        _batteryLabel = root.Q<Label>("battery-bar-label");
        _batteryValueLabel = root.Q<Label>("battery-value-label");

        // Кнопки управления
        _btnMode = root.Q<Button>("btn-mode");
        _btnPower = root.Q<Button>("btn-power");
        _btnMode.clicked += OnModeButtonClick;
        _btnPower.clicked += OnPowerButtonClick;

        // Кнопка режима дозиметра (левый цветочек)
        _btnRadiationMode = root.Q<Button>("btn-radiation-mode");
        _btnRadiationMode.clicked += OnRadiationModeButtonClick;

        // Кнопка визуальных приборов
        _btnVisualDevice = root.Q<Button>("btn-visual-device");
        _btnVisualDevice.clicked += OnVisualDeviceButtonClick;

        // Кнопка системы фильтрации
        _btnFilterSystem = root.Q<Button>("btn-filter-system");
        _btnFilterSystem.clicked += OnFilterSystemButtonClick;

        // 🔥 Кнопки отладки
        _btnDebugRadiation = root.Q<Button>("btn-debug-radiation");
        _btnDebugEvents = root.Q<Button>("btn-debug-events");
        _btnDebugRadiation.clicked += OnDebugRadiationClick;
        _btnDebugEvents.clicked += OnDebugEventsClick;

        UpdateButtonLabels();

        // Кэшируем ECS запросы
        _moveQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<MoveCommand>());
        _heroQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<GridCoordinates>(), ComponentType.ReadOnly<UnitIdComponent>());
        _zoneQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ZoneModeTag>());        
        _mapQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<GridMapTag>());
        _gridConfigQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<GridConfig>());
        _radiationConfigQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<ZoneRadiationConfig>());
        _radQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<HeroRadiationData>());
    }

    void Update()
    {
        if (_zoneQuery.IsEmpty || _heroQuery.IsEmpty || _mapQuery.IsEmpty || _gridConfigQuery.IsEmpty || _moveQuery.IsEmpty)
            return;

        int2 heroPos;
        if (!TryGetHeroPos(out heroPos)) return;

        bool isMovingNow = false;
        using (var moveEntities = _moveQuery.ToEntityArray(Allocator.Temp))
        {
            if (moveEntities.Length > 0)
            {
                var move = _em.GetComponentData<MoveCommand>(moveEntities[0]);
                isMovingNow = move.IsMoving;
            }
        }

        // 🔥 ИСПРАВЛЕНО: обновляем ОДИН РАЗ когда юнит ОСТАНОВИЛСЯ
        // или когда изменились координаты (телепорт/спавн)
        bool justStopped = _wasMovingLastFrame && !isMovingNow;
        bool positionChanged = !heroPos.Equals(_lastHeroPos);

        if (justStopped || (positionChanged && !isMovingNow))
        {
            // Списываем энергию за перемещение
            if (positionChanged && !heroPos.Equals(new int2(-9999, -9999)))
            {
                ConsumeBatteryForMovement();
            }

            UpdateLeftFlower(heroPos);
            UpdateRightFlower(heroPos);
            _lastHeroPos = heroPos;
            MarkRadiationDirty();
            MarkEventsDirty();
            UpdateHeroRadiation();
        }

        // Обновляем UI аккумулятора
        UpdateBatteryUI();

        _wasMovingLastFrame = isMovingNow;
    }

    void MarkRadiationDirty()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(RadiationDebugState));
        if (query.IsEmpty) return;
        var e = query.GetSingletonEntity();
        var state = em.GetComponentData<RadiationDebugState>(e);
        state.Dirty = true;
        em.SetComponentData(e, state);
    }

    void MarkEventsDirty()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(EventDebugState));
        if (query.IsEmpty) return;
        var e = query.GetSingletonEntity();
        var state = em.GetComponentData<EventDebugState>(e);
        state.Dirty = true;
        em.SetComponentData(e, state);
    }

    // ══════════════════════════════════════════════════════════════
    // КНОПКИ УПРАВЛЕНИЯ
    // ══════════════════════════════════════════════════════════════

    void OnModeButtonClick()
    {
        // Проверяем наличие энергии для переключения
        var newMode = (DetectorMode)(((int)_mode + 1) % 4);
        float cost = ModePowerCost[(int)newMode];

        if (!ConsumeBattery(cost))
        {
            Debug.Log("Недостаточно энергии для переключения режима!");
            return;
        }

        // Цикл: Off → MultiCell → SingleCell → ArcMode → Off
        _mode = newMode;
        UpdateButtonLabels();

        int2 heroPos;
        if (!TryGetHeroPos(out heroPos)) return;
        UpdateRightFlower(heroPos);
    }

    void OnPowerButtonClick()
    {
        // Проверяем будущую стоимость
        int newPower = (_power % 6) + 1;
        float newPowerCost = _mode != DetectorMode.Off ? PowerLevelCost[newPower - 1] : 0f;

        // Если детектор включен, проверяем хватит ли энергии на новую мощность
        if (_mode != DetectorMode.Off && newPowerCost > 0)
        {
            var query = _em.CreateEntityQuery(typeof(BatteryData), typeof(ZoneModeTag));
            if (!query.IsEmpty)
            {
                var battery = query.GetSingleton<BatteryData>();
                float requiredEnergy = ModePowerCost[(int)_mode] + newPowerCost;

                if (battery.CurrentCharge < requiredEnergy)
                {
                    Debug.Log($"Недостаточно энергии для Power {newPower}! Требуется {requiredEnergy}, доступно {battery.CurrentCharge}");
                    return;
                }
            }
        }

        // Цикл: 1 → 2 → 3 → 4 → 5 → 6 → 1
        _power = newPower;
        UpdateButtonLabels();

        int2 heroPos;
        if (!TryGetHeroPos(out heroPos)) return;
        UpdateRightFlower(heroPos);
    }


    void OnRadiationModeButtonClick()
    {
        // Проверяем наличие энергии для переключения
        var newMode = (RadiationMode)(((int)_radiationMode + 1) % 5);
        float cost = RadiationModeCost[(int)newMode];

        if (!ConsumeBattery(cost))
        {
            Debug.Log("Недостаточно энергии для переключения режима дозиметра!");
            return;
        }

        // Цикл: Off → MultiCell → PowerCell → SingleCell → ArcRad → Off
        _radiationMode = newMode;
        UpdateButtonLabels();

        int2 heroPos;
        if (!TryGetHeroPos(out heroPos)) return;
        UpdateLeftFlower(heroPos);
    }

    void OnVisualDeviceButtonClick()
    {
        var newMode = (VisualDeviceMode)(((int)_visualDevice + 1) % 3);
        float cost = VisualDeviceCost[(int)newMode];

        if (!ConsumeBattery(cost))
        {
            Debug.Log("Недостаточно энергии для переключения визуального прибора!");
            return;
        }

        // Цикл: Off → EVBinoculars → ThermoImager → Off
        _visualDevice = newMode;
        UpdateButtonLabels();

        int2 heroPos;
        if (!TryGetHeroPos(out heroPos)) return;
        UpdateRightFlower(heroPos);
    }

    void OnFilterSystemButtonClick()
    {
        var newMode = (FilterMode)(((int)_filterMode + 1) % 4);
        float cost = FilterEnergyCost[(int)newMode];

        if (!ConsumeBattery(cost))
        {
            Debug.Log("Недостаточно энергии для переключения системы фильтрации!");
            return;
        }

        _filterMode = newMode;
        UpdateButtonLabels();
    }

    // ══════════════════════════════════════════════════════════════
    // КНОПКИ ОТЛАДКИ
    // ══════════════════════════════════════════════════════════════

    void OnDebugRadiationClick()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(RadiationDebugState));
        if (query.IsEmpty) return;

        var e = query.GetSingletonEntity();
        var state = em.GetComponentData<RadiationDebugState>(e);
        state.RevealAll = !state.RevealAll;
        state.Dirty = true;
        em.SetComponentData(e, state);

        _btnDebugRadiation.text = state.RevealAll
            ? "🔒 Hide All Radiation"
            : "🔍 Show All Radiation";
    }

    void OnDebugEventsClick()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(EventDebugState));
        if (query.IsEmpty) return;

        var e = query.GetSingletonEntity();
        var state = em.GetComponentData<EventDebugState>(e);
        state.ShowAll = !state.ShowAll;
        state.Dirty = true;
        em.SetComponentData(e, state);

        _btnDebugEvents.text = state.ShowAll
            ? "🔒 Hide All Events"
            : "🔍 Show All Events";
    }

    public void UpdateHeroRadiation()
    {
        float radiation01;
        var entities = _radQuery.ToEntityArray(Allocator.Temp);
        radiation01 = _em.GetComponentData<HeroRadiationData>(entities[0]).TotalRadiation;

        Debug.Log($"[ZoneUI] Updating hero radiation display: {radiation01}");

        float percent = radiation01;
        _radiationFill.style.height = Length.Percent(percent);
        _radiationLabel.text = $"{math.round(percent)}%";

        // 🔥 Обновляем цифру под полосой
        if (_radiationValueLabel != null)
        {
            _radiationValueLabel.text = $"{math.round(percent)}";
        }

        // Цвет — по порогам
        _radiationFill.style.backgroundColor = percent switch
        {
            < 25f => new Color(0.3f, 1f, 0.3f),
            < 50f => new Color(1f, 1f, 0.3f),
            < 75f => new Color(1f, 0.6f, 0.2f),
            _ => new Color(1f, 0.2f, 0.2f)
        };

        entities.Dispose();
    }


    void UpdateButtonLabels()
    {
        // Детектор (правый цветочек)
        string modeText = "";
        float modeCost = ModePowerCost[(int)_mode];
        switch (_mode)
        {
            case DetectorMode.Off: modeText = "OFF"; break;
            case DetectorMode.MultiCell: modeText = "MULTI"; break;
            case DetectorMode.SingleCell: modeText = "SINGLE"; break;
            case DetectorMode.ArcMode: modeText = "ARC"; break;
        }
        _btnMode.text = modeCost > 0 ? $"{modeText} (-{modeCost}⚡)" : modeText;

        // 🔥 ИЗМЕНЕНО: Показываем стоимость Power
        float powerCost = _mode != DetectorMode.Off ? PowerLevelCost[_power - 1] : 0f;
        _btnPower.text = powerCost > 0 ? $"PWR:{_power} (-{powerCost}⚡)" : $"PWR:{_power}";

        // Дозиметр (левый цветочек)
        string radModeText = "";
        float radCost = RadiationModeCost[(int)_radiationMode];
        switch (_radiationMode)
        {
            case RadiationMode.Off: radModeText = "OFF"; break;
            case RadiationMode.MultiCell: radModeText = "MULTI"; break;
            case RadiationMode.PowerCell: radModeText = "POWER"; break;
            case RadiationMode.SingleCell: radModeText = "SINGLE"; break;
            case RadiationMode.ArcRad: radModeText = "ARC"; break;
        }
        _btnRadiationMode.text = radCost > 0 ? $"{radModeText} (-{radCost}⚡)" : radModeText;

        // Визуальные приборы
        string visualText = "";
        float visualCost = VisualDeviceCost[(int)_visualDevice];
        int detectionBonus = VisualDeviceDetectionBonus[(int)_visualDevice];
        switch (_visualDevice)
        {
            case VisualDeviceMode.Off: visualText = "Visual: OFF"; break;
            case VisualDeviceMode.EVBinoculars: visualText = $"EV Binoculars (-{visualCost}⚡, +{detectionBonus}🔍)"; break;
            case VisualDeviceMode.ThermoImager: visualText = $"ThermoImager (-{visualCost}⚡, +{detectionBonus}🔍)"; break;
        }
        _btnVisualDevice.text = visualText;

        // Система очистки фильтров
        string filterText = "";
        int radReduction = FilterRadReduction[(int)_filterMode];
        float filterCost = FilterEnergyCost[(int)_filterMode];
        switch (_filterMode)
        {
            case FilterMode.Off:
                filterText = "Filter: OFF";
                break;
            case FilterMode.StdFiltering:
                filterText = $"Std Filtering (-{radReduction}☢ / -{filterCost}⚡)";
                break;
            case FilterMode.MaxFiltering:
                filterText = $"Max Filtering (-{radReduction}☢ / -{filterCost}⚡)";
                break;
            case FilterMode.OverloadFiltering:
                filterText = $"Overload Filtering (-{radReduction}☢ / -{filterCost}⚡)";
                break;
        }
        _btnFilterSystem.text = filterText;
    }


    // ══════════════════════════════════════════════════════════════
    // ЛЕВЫЙ ЦВЕТОЧЕК — радиация (дозиметр)
    // ══════════════════════════════════════════════════════════════

    void UpdateLeftFlower(int2 heroPos)
    {
        switch (_radiationMode)
        {
            case RadiationMode.Off: DrawRadiationOff(); break;
            case RadiationMode.MultiCell: DrawRadiationMultiCell(heroPos); break;
            case RadiationMode.PowerCell: DrawRadiationPowerCell(heroPos); break;
            case RadiationMode.SingleCell: DrawRadiationSingleCell(heroPos); break;
            case RadiationMode.ArcRad: DrawRadiationArcRad(heroPos); break;
        }
    }

    // ── OFF режим дозиметра ────────────────────────────────────────
    void DrawRadiationOff()
    {
        for (int i = 0; i < 7; i++)
        {
            _leftHexes[i].style.backgroundColor = ColorOff;
            _leftLabels[i].text = "";
        }
    }

    // ── MULTI CELL режим (базовый, как было раньше) ────────────────
    void DrawRadiationMultiCell(int2 heroPos)
    {
        if (_radiationConfigQuery.IsEmpty) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var buf = _em.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        var config = _radiationConfigQuery.GetSingleton<ZoneRadiationConfig>();
        var grid = _gridConfigQuery.GetSingleton<GridConfig>();

        for (int i = 0; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int rad = GetRadiationAt(pos, buf, grid.GridSize);
            _leftLabels[i].text = rad >= 0 ? rad.ToString() : "—";
            _leftHexes[i].style.backgroundColor = GetRadiationColor(rad, config);
        }
    }

    // ── POWER CELL режим ───────────────────────────────────────────
    void DrawRadiationPowerCell(int2 heroPos)
    {
        if (_radiationConfigQuery.IsEmpty) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var buf = _em.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        var config = _radiationConfigQuery.GetSingleton<ZoneRadiationConfig>();
        var grid = _gridConfigQuery.GetSingleton<GridConfig>();

        int currentRad = GetRadiationAt(heroPos, buf, grid.GridSize);

        _leftLabels[0].text = currentRad >= 0 ? currentRad.ToString() : "—";
        _leftHexes[0].style.backgroundColor = GetRadiationColor(currentRad, config);

        for (int i = 1; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int rad = GetRadiationAt(pos, buf, grid.GridSize);

            if (rad > currentRad && rad >= 0)
            {
                _leftLabels[i].text = rad.ToString();
                _leftHexes[i].style.backgroundColor = GetRadiationColor(rad, config);
            }
            else
            {
                _leftLabels[i].text = "";
                _leftHexes[i].style.backgroundColor = ColorOff;
            }
        }
    }

    // ── SINGLE CELL режим ──────────────────────────────────────────
    // 🔥 ИСПРАВЛЕНО: Показывает ОДНУ случайную клетку из САМЫХ МОЩНЫХ
    void DrawRadiationSingleCell(int2 heroPos)
    {
        if (_radiationConfigQuery.IsEmpty) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var buf = _em.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        var config = _radiationConfigQuery.GetSingleton<ZoneRadiationConfig>();
        var grid = _gridConfigQuery.GetSingleton<GridConfig>();

        int currentRad = GetRadiationAt(heroPos, buf, grid.GridSize);

        _leftLabels[0].text = currentRad >= 0 ? currentRad.ToString() : "—";
        _leftHexes[0].style.backgroundColor = GetRadiationColor(currentRad, config);

        // Находим максимальную радиацию среди соседей
        int maxRad = currentRad;
        for (int i = 1; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int rad = GetRadiationAt(pos, buf, grid.GridSize);
            if (rad > maxRad)
            {
                maxRad = rad;
            }
        }

        // Собираем ВСЕ направления с максимальной радиацией
        var maxRadDirections = new System.Collections.Generic.List<int>();
        for (int i = 1; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int rad = GetRadiationAt(pos, buf, grid.GridSize);
            if (rad == maxRad && rad > currentRad)
            {
                maxRadDirections.Add(i);
            }
        }

        for (int i = 1; i < 7; i++)
        {
            _leftHexes[i].style.backgroundColor = ColorOff;
            _leftLabels[i].text = "";
        }

        if (maxRadDirections.Count > 0)
        {
            int randomIndex = maxRadDirections[UnityEngine.Random.Range(0, maxRadDirections.Count)];
            int2 pos = heroPos + HexOffsets[randomIndex];
            int rad = GetRadiationAt(pos, buf, grid.GridSize);

            _leftLabels[randomIndex].text = rad.ToString();
            _leftHexes[randomIndex].style.backgroundColor = GetRadiationColor(rad, config);
        }
    }

    // ── ARC RAD режим ──────────────────────────────────────────────
    // 🔥 ИСПРАВЛЕНО: Все три клетки имеют одинаковый цвет (цвет настоящей клетки)
    void DrawRadiationArcRad(int2 heroPos)
    {
        if (_radiationConfigQuery.IsEmpty) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var buf = _em.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        var config = _radiationConfigQuery.GetSingleton<ZoneRadiationConfig>();
        var grid = _gridConfigQuery.GetSingleton<GridConfig>();

        int currentRad = GetRadiationAt(heroPos, buf, grid.GridSize);

        _leftLabels[0].text = currentRad >= 0 ? currentRad.ToString() : "—";
        _leftHexes[0].style.backgroundColor = GetRadiationColor(currentRad, config);

        // Находим максимальную радиацию
        int maxRad = currentRad;
        for (int i = 1; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int rad = GetRadiationAt(pos, buf, grid.GridSize);
            if (rad > maxRad)
            {
                maxRad = rad;
            }
        }

        var maxRadDirections = new System.Collections.Generic.List<int>();
        for (int i = 1; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int rad = GetRadiationAt(pos, buf, grid.GridSize);
            if (rad == maxRad && rad > currentRad)
            {
                maxRadDirections.Add(i);
            }
        }

        for (int i = 1; i < 7; i++)
        {
            _leftHexes[i].style.backgroundColor = ColorOff;
            _leftLabels[i].text = "";
        }

        if (maxRadDirections.Count > 0)
        {
            int mainDir = maxRadDirections[UnityEngine.Random.Range(0, maxRadDirections.Count)];
            int2 mainPos = heroPos + HexOffsets[mainDir];
            int mainRad = GetRadiationAt(mainPos, buf, grid.GridSize);

            Color realCellColor = GetRadiationColor(mainRad, config);

            _leftLabels[mainDir].text = mainRad.ToString();
            _leftHexes[mainDir].style.backgroundColor = realCellColor;

            int left = ((mainDir - 2 + 6) % 6) + 1;
            int right = (mainDir % 6) + 1;

            if (UnityEngine.Random.value < 0.5f)
            {
                _leftHexes[left].style.backgroundColor = realCellColor;
                _leftHexes[right].style.backgroundColor = realCellColor;
            }
            else
            {
                int dir = UnityEngine.Random.value < 0.5f ? 1 : -1;
                int idx1 = (mainDir + dir - 1 + 6) % 6 + 1;
                int idx2 = (mainDir + 2 * dir - 1 + 6) % 6 + 1;

                _leftHexes[idx1].style.backgroundColor = realCellColor;
                _leftHexes[idx2].style.backgroundColor = realCellColor;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    // ПРАВЫЙ ЦВЕТОЧЕК — детектор
    // ══════════════════════════════════════════════════════════════

    void UpdateRightFlower(int2 heroPos)
    {
        switch (_mode)
        {
            case DetectorMode.Off: DrawDetectorOff(); break;
            case DetectorMode.MultiCell: DrawDetectorMultiCell(heroPos); break;
            case DetectorMode.SingleCell: DrawDetectorSingleCell(heroPos); break;
            case DetectorMode.ArcMode: DrawDetectorArcMode(heroPos); break;
        }
    }

    // ── OFF режим ──────────────────────────────────────────────────
    void DrawDetectorOff()
    {
        for (int i = 0; i < 7; i++)
        {
            _rightHexes[i].style.backgroundColor = ColorOff;
            _rightLabels[i].text = "";
        }
    }

    // ── MULTI режим: лучи во все 6 направлений ─────────────────────
    void DrawDetectorMultiCell(int2 heroPos)
    {
        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneEventElement>(mapEntity))
        {
            DrawDetectorOff();
            return;
        }

        var events = _em.GetBuffer<ZoneEventElement>(mapEntity, true);
        var grid = _gridConfigQuery.GetSingleton<GridConfig>();

        _rightHexes[0].style.backgroundColor = ColorOff;
        _rightLabels[0].text = "";

        for (int i = 1; i < 7; i++)
        {
            int dist = ScanDirectionWithPower(heroPos, HexOffsets[i], events, grid.GridSize);

            if (dist < 0)
            {
                _rightHexes[i].style.backgroundColor = ColorEmpty;
                _rightLabels[i].text = "—";
            }
            else
            {
                _rightLabels[i].text = dist.ToString();
                _rightHexes[i].style.backgroundColor = GetAnomalyDistColor(dist);
            }
        }
    }

    // ── ARC режим: одна ближайшая аномалия + 2 случайных соседа ────
    void DrawDetectorArcMode(int2 heroPos)
    {
        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneEventElement>(mapEntity))
        {
            DrawDetectorOff();
            return;
        }

        var events = _em.GetBuffer<ZoneEventElement>(mapEntity, true);
        var grid = _gridConfigQuery.GetSingleton<GridConfig>();

        int nearestDist = -1;
        int nearestDir = -1;

        for (int i = 1; i < 7; i++)
        {
            int dist = ScanDirectionWithPower(heroPos, HexOffsets[i], events, grid.GridSize);
            if (dist >= 0 && (nearestDist < 0 || dist < nearestDist))
            {
                nearestDist = dist;
                nearestDir = i;
            }
        }

        for (int i = 0; i < 7; i++)
        {
            _rightHexes[i].style.backgroundColor = ColorOff;
            _rightLabels[i].text = "";
        }

        if (nearestDir < 0)
        {
            _rightLabels[0].text = "—";
            return;
        }

        _rightLabels[0].text = nearestDist.ToString();
        _rightHexes[0].style.backgroundColor = GetAnomalyDistColor(nearestDist);

        _rightHexes[nearestDir].style.backgroundColor = GetAnomalyDistColor(nearestDist);

        int left = ((nearestDir - 2 + 6) % 6) + 1;
        int right = (nearestDir % 6) + 1;

        if (UnityEngine.Random.value < 0.5f)
        {
            _rightHexes[left].style.backgroundColor = GetAnomalyDistColor(nearestDist);
            _rightHexes[right].style.backgroundColor = GetAnomalyDistColor(nearestDist);
        }
        else
        {
            int dir = UnityEngine.Random.value < 0.5f ? 1 : -1;
            int idx1 = (nearestDir + dir - 1 + 6) % 6 + 1;
            int idx2 = (nearestDir + 2 * dir - 1 + 6) % 6 + 1;

            _rightHexes[idx1].style.backgroundColor = GetAnomalyDistColor(nearestDist);
            _rightHexes[idx2].style.backgroundColor = GetAnomalyDistColor(nearestDist);
        }
    }

    // ══════════════════════════════════════════════════════════════
    // СКАНИРОВАНИЕ ЛУЧА С УЧЁТОМ МОЩНОСТИ
    // ══════════════════════════════════════════════════════════════

    int ScanDirectionWithPower(int2 heroPos, int2 dir, DynamicBuffer<ZoneEventElement> events, int2 gridSize)
    {
        int effectivePowerBase = _power + VisualDeviceDetectionBonus[(int)_visualDevice];
        int maxDist = Mathf.Min(effectivePowerBase, MAX_SCAN_RANGE);

        for (int step = 1; step <= maxDist; step++)
        {
            int2 current = heroPos + dir * step;
            if (!HexGridUtils.IsHexInBounds(current, gridSize))
                return -1;

            for (int e = 0; e < events.Length; e++)
            {
                var evt = events[e];
                if (evt.EventType != ZoneEventType.Anomaly) continue;
                if (evt.GridPos.x != current.x || evt.GridPos.y != current.y) continue;

                int effectivePower = effectivePowerBase - step - evt.Visibility;
                if (effectivePower >= 0)
                {
                    return step;
                }
            }
        }

        return -1;
    }

    Color GetAnomalyDistColor(int dist)
    {
        if (dist <= 2) return ColorAnomalyNear;
        if (dist <= 5) return ColorAnomalyMid;
        return ColorAnomalyFar;
    }

    // ══════════════════════════════════════════════════════════════
    // SINGLE CELL РЕЖИМ
    // ══════════════════════════════════════════════════════════════

    void DrawDetectorSingleCell(int2 heroPos)
    {
        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneEventElement>(mapEntity))
        {
            DrawDetectorOff();
            return;
        }

        var events = _em.GetBuffer<ZoneEventElement>(mapEntity, true);
        var grid = _gridConfigQuery.GetSingleton<GridConfig>();

        int nearestDist = -1;
        int nearestDir = -1;

        for (int i = 1; i < 7; i++)
        {
            int dist = ScanDirectionWithPower(heroPos, HexOffsets[i], events, grid.GridSize);
            if (dist >= 0 && (nearestDist < 0 || dist < nearestDist))
            {
                nearestDist = dist;
                nearestDir = i;
            }
        }

        for (int i = 0; i < 7; i++)
        {
            _rightHexes[i].style.backgroundColor = ColorOff;
            _rightLabels[i].text = "";
        }

        if (nearestDir < 0)
        {
            _rightLabels[0].text = "—";
            return;
        }

        _rightLabels[0].text = nearestDist.ToString();
        _rightHexes[0].style.backgroundColor = GetAnomalyDistColor(nearestDist);

        _rightHexes[nearestDir].style.backgroundColor = GetAnomalyDistColor(nearestDist);
    }

    // ══════════════════════════════════════════════════════════════
    // УПРАВЛЕНИЕ АККУМУЛЯТОРОМ
    // ══════════════════════════════════════════════════════════════

    bool ConsumeBattery(float amount)
    {
        if (amount <= 0) return true;

        var query = _em.CreateEntityQuery(typeof(BatteryData));
        if (query.IsEmpty) return false;

        var entity = query.GetSingletonEntity();
        var battery = _em.GetComponentData<BatteryData>(entity);

        if (battery.CurrentCharge >= amount)
        {
            battery.CurrentCharge -= amount;
            _em.SetComponentData(entity, battery);
            return true;
        }

        return false;
    }

    void ConsumeBatteryForMovement()
    {
        float totalCost = 0f;

        // Детектор: режим + мощность
        totalCost += ModePowerCost[(int)_mode];
        if (_mode != DetectorMode.Off)  // Мощность учитывается только если детектор включен
        {
            totalCost += PowerLevelCost[_power - 1];  // _power это 1..6, а массив 0..5
        }

        totalCost += RadiationModeCost[(int)_radiationMode];
        totalCost += VisualDeviceCost[(int)_visualDevice];
        totalCost += FilterEnergyCost[(int)_filterMode];

        if (totalCost > 0)
        {
            bool success = ConsumeBattery(totalCost);

            if (!success)
            {
                Debug.Log("[Battery] Энергия закончилась! Все приборы выключены.");

                _mode = DetectorMode.Off;
                _radiationMode = RadiationMode.MultiCell;
                _visualDevice = VisualDeviceMode.Off;
                _filterMode = FilterMode.Off;

                UpdateButtonLabels();

                int2 heroPos;
                if (TryGetHeroPos(out heroPos))
                {
                    UpdateLeftFlower(heroPos);
                    UpdateRightFlower(heroPos);
                }
            }
            else
            {
                float detectorCost = ModePowerCost[(int)_mode] + (_mode != DetectorMode.Off ? PowerLevelCost[_power - 1] : 0f);
                Debug.Log($"[Battery] Consumed {totalCost} energy for movement (detector: {detectorCost} [mode:{ModePowerCost[(int)_mode]} + pwr:{(_mode != DetectorMode.Off ? PowerLevelCost[_power - 1] : 0f)}], dosimeter: {RadiationModeCost[(int)_radiationMode]}, visual: {VisualDeviceCost[(int)_visualDevice]}, filter: {FilterEnergyCost[(int)_filterMode]})");

                if (_filterMode != FilterMode.Off)
                {
                    ApplyRadiationReduction();
                }
            }
        }
    }


    void ApplyRadiationReduction()
    {
        int reduction = FilterRadReduction[(int)_filterMode];
        if (reduction <= 0) return;

        var entities = _radQuery.ToEntityArray(Allocator.Temp);
        if (entities.Length == 0)
        {
            entities.Dispose();
            return;
        }

        var radData = _em.GetComponentData<HeroRadiationData>(entities[0]);
        int oldRad = radData.TotalRadiation;
        radData.TotalRadiation = Mathf.Max(0, radData.TotalRadiation - reduction);
        _em.SetComponentData(entities[0], radData);

        Debug.Log($"[Filter] Reduced radiation from {oldRad} to {radData.TotalRadiation} (reduction: {reduction})");

        entities.Dispose();

        // Обновляем UI радиации
        UpdateHeroRadiation();
    }

    void UpdateBatteryUI()
    {
        var query = _em.CreateEntityQuery(typeof(BatteryData), typeof(ZoneModeTag));
        if (query.IsEmpty) return;

        var battery = query.GetSingleton<BatteryData>();

        float percentage = battery.CurrentCharge / battery.MaxCharge;
        percentage = Mathf.Clamp01(percentage);

        _batteryFill.style.height = Length.Percent(percentage * 100f);
        _batteryLabel.text = $"{Mathf.RoundToInt(percentage * 100f)}%";

        // 🔥 Обновляем цифру под полосой
        if (_batteryValueLabel != null)
        {
            _batteryValueLabel.text = $"{Mathf.RoundToInt(battery.CurrentCharge)}/{Mathf.RoundToInt(battery.MaxCharge)}";
        }

        // Цвет по уровню заряда
        Color fillColor;
        if (percentage > 0.5f)
            fillColor = new Color(0.4f, 0.8f, 1f, 0.9f); // Синий
        else if (percentage > 0.25f)
            fillColor = new Color(1f, 0.8f, 0.2f, 0.9f); // Жёлтый
        else
            fillColor = new Color(1f, 0.3f, 0.2f, 0.9f); // Красный

        _batteryFill.style.backgroundColor = fillColor;
    }


    // ══════════════════════════════════════════════════════════════
    // УТИЛИТЫ
    // ══════════════════════════════════════════════════════════════

    bool TryGetHeroPos(out int2 pos)
    {
        pos = default;
        var entities = _heroQuery.ToEntityArray(Allocator.Temp);
        bool found = false;

        for (int i = 0; i < entities.Length; i++)
        {
            if (_em.GetComponentData<UnitIdComponent>(entities[i]).UnitId == 0)
            {
                pos = _em.GetComponentData<GridCoordinates>(entities[i]).Value;
                found = true;
                break;
            }
        }

        entities.Dispose();
        return found;
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
        if (radiation < 0) return new Color(0.3f, 0.3f, 0.3f, 0.5f);

        Color c;
        if (radiation <= cfg.LevelGreen) c = new Color(cfg.ColorGreen.x, cfg.ColorGreen.y, cfg.ColorGreen.z);
        else if (radiation <= cfg.LevelYellow) c = new Color(cfg.ColorYellow.x, cfg.ColorYellow.y, cfg.ColorYellow.z);
        else if (radiation <= cfg.LevelOrange) c = new Color(cfg.ColorOrange.x, cfg.ColorOrange.y, cfg.ColorOrange.z);
        else c = new Color(cfg.ColorRed.x, cfg.ColorRed.y, cfg.ColorRed.z);

        c.a = 0.8f;
        return c;
    }
}
