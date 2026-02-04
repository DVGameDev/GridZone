using Unity.Collections;
using Unity.Entities;
using Unity.Entities.UniversalDelegates;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Контроллер двух цветочков.
/// Левый  — радиация вокруг героя.
/// Правый — детектор аномалий с 3 режимами: Off / MultiCell / ArcMode.
/// </summary>
public class ZoneUIController : MonoBehaviour
{
    public UIDocument uiDocument;
    VisualElement _radiationFill;
    Label _radiationLabel;
    
    // ── Аккумулятор ────────────────────────────────────────────────
    VisualElement _batteryFill;
    Label _batteryLabel;

    float _cachedRadiation = -1f;

    // ── cached UI refs ────────────────────────────────────────────
    private VisualElement[] _leftHexes   = new VisualElement[7];
    private Label[]         _leftLabels  = new Label[7];
    private VisualElement[] _rightHexes  = new VisualElement[7];
    private Label[]         _rightLabels = new Label[7];

    private Button _btnMode;
    private Button _btnPower;
    private Button _btnDebugRadiation;
    private Button _btnDebugEvents;
    
    // ── Кнопка режима дозиметра (левый цветочек) ──────────────────
    private Button _btnRadiationMode;

    // ── cached ECS queries ────────────────────────────────────────
    private EntityManager _em;
    private EntityQuery _moveQuery;
    private EntityQuery _radQuery;
    private EntityQuery   _zoneQuery;
    private EntityQuery   _heroQuery;
    private EntityQuery   _mapQuery;
    private EntityQuery   _gridConfigQuery;
    private EntityQuery   _radiationConfigQuery;

    // ── детектор: режим и мощность ─────────────────────────────────
    private enum DetectorMode { Off, MultiCell, SingleCell, ArcMode }
    private DetectorMode _mode = DetectorMode.Off;
    private int _power = 1; // 1..6
    
    // ── Энергопотребление детектора ──────────────────────────────
    private static readonly float[] ModePowerCost = new float[] 
    { 
        0f,    // Off - не потребляет
        2f,    // MultiCell - 2 энергии
        1f,    // SingleCell - 1 энергия
        3f     // ArcMode - 3 энергии
    };
    
    // ── дозиметр: режим ─────────────────────────────────────────────
    private enum RadiationMode { Off, MultiCell, PowerCell, SingleCell, ArcRad }
    private RadiationMode _radiationMode = RadiationMode.MultiCell;
    
    // ── Энергопотребление дозиметра ──────────────────────────────
    private static readonly float[] RadiationModeCost = new float[] 
    { 
        0f,    // Off - не потребляет
        0f,    // MultiCell - бесплатно (базовый режим)
        3f,    // PowerCell - 3 энергии
        2f,    // SingleCell - 2 энергии
        3f     // ArcRad - 3 энергии
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
    private static readonly Color ColorOff          = new Color(0.15f, 0.15f, 0.15f, 0.6f);
    private static readonly Color ColorEmpty        = new Color(0.22f, 0.22f, 0.30f, 0.7f);
    private static readonly Color ColorAnomalyFar   = new Color(0.7f, 0.2f, 0.9f, 0.7f);
    private static readonly Color ColorAnomalyMid   = new Color(0.9f, 0.5f, 0.1f, 0.8f);
    private static readonly Color ColorAnomalyNear  = new Color(1.0f, 0.15f, 0.15f, 0.9f);
    private static readonly Color ColorArcHighlight = new Color(0.3f, 0.6f, 0.9f, 0.6f); // подсветка соседей в ArcMode

    private const int MAX_SCAN_RANGE = 50;


    void Start()
    {
        _em = World.DefaultGameObjectInjectionWorld.EntityManager;

        var root = uiDocument.rootVisualElement;

        // Кэшируем хексы
        for (int i = 0; i < 7; i++)
        {
            _leftHexes[i]   = root.Q<VisualElement>($"left-hex-{i}");
            _leftLabels[i]  = root.Q<Label>($"left-label-{i}");
            _rightHexes[i]  = root.Q<VisualElement>($"right-hex-{i}");
            _rightLabels[i] = root.Q<Label>($"right-label-{i}");
        }

        _radiationFill = root.Q<VisualElement>("radiation-bar-fill");
        _radiationLabel = root.Q<Label>("radiation-bar-label");
        
        // Аккумулятор
        _batteryFill = root.Q<VisualElement>("battery-bar-fill");
        _batteryLabel = root.Q<Label>("battery-bar-label");

        // Кнопки управления
        _btnMode = root.Q<Button>("btn-mode");
        _btnPower = root.Q<Button>("btn-power");

        _btnMode.clicked  += OnModeButtonClick;
        _btnPower.clicked += OnPowerButtonClick;
        
        // Кнопка режима дозиметра (левый цветочек)
        _btnRadiationMode = root.Q<Button>("btn-radiation-mode");
        _btnRadiationMode.clicked += OnRadiationModeButtonClick;
        
        // 🔥 Кнопки отладки
        _btnDebugRadiation = root.Q<Button>("btn-debug-radiation");
        _btnDebugEvents = root.Q<Button>("btn-debug-events");
        
        _btnDebugRadiation.clicked += OnDebugRadiationClick;
        _btnDebugEvents.clicked += OnDebugEventsClick;

        UpdateButtonLabels();

        // Кэшируем ECS запросы
        _moveQuery = _em.CreateEntityQuery(ComponentType.ReadOnly<MoveCommand>());
        _zoneQuery            = _em.CreateEntityQuery(ComponentType.ReadOnly<ZoneModeTag>());
        _heroQuery            = _em.CreateEntityQuery(ComponentType.ReadOnly<GridCoordinates>(), ComponentType.ReadOnly<UnitIdComponent>());
        _mapQuery             = _em.CreateEntityQuery(ComponentType.ReadOnly<GridMapTag>());
        _gridConfigQuery      = _em.CreateEntityQuery(ComponentType.ReadOnly<GridConfig>());
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
    //  КНОПКИ УПРАВЛЕНИЯ
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
        // Цикл: 1 → 2 → 3 → 4 → 5 → 6 → 1
        _power = (_power % 6) + 1;
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

    // ══════════════════════════════════════════════════════════════
    //  КНОПКИ ОТЛАДКИ
    // ══════════════════════════════════════════════════════════════
    void OnDebugRadiationClick()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(RadiationDebugState));
        if (query.IsEmpty) return;

        var e = query.GetSingletonEntity();
        var state = em.GetComponentData<RadiationDebugState>(e);

        state.RevealAll = !state.RevealAll;
        state.Dirty = true; // 🔥 важно

        em.SetComponentData(e, state);

        _btnDebugRadiation.text = state.RevealAll
            ? "🔒 Hide All Radiation"
            : "🔍 Show All Radiation";
    }

    /*
    void OnDebugRadiationClick()
    {
        _debugRadiationEnabled = !_debugRadiationEnabled;
        
        if (_debugRadiationEnabled)
        {
            _btnDebugRadiation.text = "🔒 Hide All Radiation";
            
            //RevealAllRadiation();
        }
        else
        {
            _btnDebugRadiation.text = "🔍 Show All Radiation";
            // НЕ скрываем уже открытые клетки!
        }
    }
    */

    void OnDebugEventsClick()
    {
        var em = World.DefaultGameObjectInjectionWorld.EntityManager;
        var query = em.CreateEntityQuery(typeof(EventDebugState));
        if (query.IsEmpty) return;
        var e = query.GetSingletonEntity();
        var state = em.GetComponentData<EventDebugState>(e);
        state.ShowAll = !state.ShowAll;
        state.Dirty = true; // 🔥 ДОБАВИТЬ
        em.SetComponentData(e, state);
        _btnDebugEvents.text = state.ShowAll
            ? "🔒 Hide All Events"
            : "🔍 Show All Events";
    }


    /*
    void RevealAllRadiation()
    {
        if (!SystemAPI.HasSingleton<GridMapTag>()) return;
        
        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        if (!_em.HasBuffer<ZoneCellRadiation>(mapEntity)) return;
        
        var radiationBuffer = _em.GetBuffer<ZoneCellRadiation>(mapEntity);
        var radiationConfig = SystemAPI.GetSingleton<ZoneRadiationConfig>();
        
        for (int i = 0; i < radiationBuffer.Length; i++)
        {
            var cell = radiationBuffer[i];
            
            // Определяем цвет по уровню радиации
            float4 cellColor;
            switch (cell.RadiationLevel)
            {
                case 0: cellColor = radiationConfig.ColorGreen; break;
                case 5: cellColor = radiationConfig.ColorYellow; break;
                case 10: cellColor = radiationConfig.ColorOrange; break;
                case 15: cellColor = radiationConfig.ColorRed; break;
                default: cellColor = radiationConfig.ColorYellow; break;
            }
            
            // Применяем цвет
            if (_em.HasComponent<URPMaterialPropertyBaseColor>(cell.CellEntity))
            {
                _em.SetComponentData(cell.CellEntity, new URPMaterialPropertyBaseColor { Value = cellColor });
            }
            
            if (_em.HasComponent<CellCustomColor>(cell.CellEntity))
            {
                _em.SetComponentData(cell.CellEntity, new CellCustomColor { BaseColor = cellColor });
            }
        }
        
        Debug.Log("[DEBUG] Revealed all radiation!");
    }

    void RevealAllEvents()
    {
        if (!SystemAPI.HasSingleton<GridMapTag>()) return;
        
        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        if (!_em.HasBuffer<ZoneEventElement>(mapEntity)) return;
        
        var eventBuffer = _em.GetBuffer<ZoneEventElement>(mapEntity);
        
        for (int i = 0; i < eventBuffer.Length; i++)
        {
            var eventElement = eventBuffer[i];
            
            // Помечаем как обнаруженное
            eventElement.IsDiscovered = true;
            eventBuffer[i] = eventElement;
            
            // Обновляем entity события
            if (_em.Exists(eventElement.EventEntity))
            {
                var eventData = _em.GetComponentData<ZoneEventData>(eventElement.EventEntity);
                eventData.IsDiscovered = true;
                _em.SetComponentData(eventElement.EventEntity, eventData);
            }
        }
        
        Debug.Log($"[DEBUG] Revealed all {eventBuffer.Length} events!");
    }
    */

    public void UpdateHeroRadiation()
    {
        float radiation01;
        var entities = _radQuery.ToEntityArray(Allocator.Temp);
        radiation01 = _em.GetComponentData<HeroRadiationData>(entities[0]).TotalRadiation;
        //radiation01 = math.clamp(radiation01, 0f, 1f);

        // 🔒 защита от лишних обновлений
       // if (math.abs(radiation01 - _cachedRadiation) < 0.001f)
       //     return;

       // _cachedRadiation = radiation01;
        Debug.Log($"[ZoneUI] Updating hero radiation display: {radiation01}");
        float percent = radiation01; // 100f;

        _radiationFill.style.height = Length.Percent(percent);
        _radiationLabel.text = $"{math.round(percent)}%";

        // Цвет — по порогам
        _radiationFill.style.backgroundColor = percent switch
        {
            < 25f => new Color(0.3f, 1f, 0.3f),
            < 50f => new Color(1f, 1f, 0.3f),
            < 75f => new Color(1f, 0.6f, 0.2f),
            _ => new Color(1f, 0.2f, 0.2f)
        };
    }

    void UpdateButtonLabels()
    {
        // Детектор (правый цветочек)
        string modeText = "";
        float cost = ModePowerCost[(int)_mode];
        
        switch (_mode)
        {
            case DetectorMode.Off:       modeText = "OFF";  break;
            case DetectorMode.MultiCell: modeText = "MULTI"; break;
            case DetectorMode.SingleCell: modeText = "SINGLE"; break;
            case DetectorMode.ArcMode:   modeText = "ARC";  break;
        }
        
        _btnMode.text = cost > 0 ? $"{modeText} (-{cost}⚡)" : modeText;
        _btnPower.text = $"PWR:{_power}";
        
        // Дозиметр (левый цветочек)
        string radModeText = "";
        float radCost = RadiationModeCost[(int)_radiationMode];
        
        switch (_radiationMode)
        {
            case RadiationMode.Off:       radModeText = "OFF";   break;
            case RadiationMode.MultiCell: radModeText = "MULTI"; break;
            case RadiationMode.PowerCell: radModeText = "POWER"; break;
            case RadiationMode.SingleCell: radModeText = "SINGLE"; break;
            case RadiationMode.ArcRad:    radModeText = "ARC";   break;
        }
        
        _btnRadiationMode.text = radCost > 0 ? $"{radModeText} (-{radCost}⚡)" : radModeText;
    }
    
    // ══════════════════════════════════════════════════════════════
    //  ЛЕВЫЙ ЦВЕТОЧЕК — радиация (дозиметр)
    // ══════════════════════════════════════════════════════════════
    void UpdateLeftFlower(int2 heroPos)
    {
        switch (_radiationMode)
        {
            case RadiationMode.Off:       DrawRadiationOff();                  break;
            case RadiationMode.MultiCell: DrawRadiationMultiCell(heroPos);     break;
            case RadiationMode.PowerCell: DrawRadiationPowerCell(heroPos);     break;
            case RadiationMode.SingleCell: DrawRadiationSingleCell(heroPos);   break;
            case RadiationMode.ArcRad:    DrawRadiationArcRad(heroPos);        break;
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

        var buf    = _em.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        var config = _radiationConfigQuery.GetSingleton<ZoneRadiationConfig>();
        var grid   = _gridConfigQuery.GetSingleton<GridConfig>();

        for (int i = 0; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int  rad = GetRadiationAt(pos, buf, grid.GridSize);

            _leftLabels[i].text = rad >= 0 ? rad.ToString() : "—";
            _leftHexes[i].style.backgroundColor = GetRadiationColor(rad, config);
        }
    }
    
    // ── POWER CELL режим ───────────────────────────────────────────
    // Показывает направления, где радиация СИЛЬНЕЕ чем на текущей клетке
    void DrawRadiationPowerCell(int2 heroPos)
    {
        if (_radiationConfigQuery.IsEmpty) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var buf    = _em.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        var config = _radiationConfigQuery.GetSingleton<ZoneRadiationConfig>();
        var grid   = _gridConfigQuery.GetSingleton<GridConfig>();

        // Получаем радиацию текущей клетки (центр)
        int currentRad = GetRadiationAt(heroPos, buf, grid.GridSize);
        
        // Центр показывает текущую радиацию
        _leftLabels[0].text = currentRad >= 0 ? currentRad.ToString() : "—";
        _leftHexes[0].style.backgroundColor = GetRadiationColor(currentRad, config);

        // Для каждого направления проверяем, есть ли там более высокая радиация
        for (int i = 1; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int  rad = GetRadiationAt(pos, buf, grid.GridSize);

            if (rad > currentRad && rad >= 0)
            {
                // Радиация сильнее - закрашиваем лепесток
                _leftLabels[i].text = rad.ToString();
                _leftHexes[i].style.backgroundColor = GetRadiationColor(rad, config);
            }
            else
            {
                // Радиация такая же или слабее - серый
                _leftLabels[i].text = "";
                _leftHexes[i].style.backgroundColor = ColorOff;
            }
        }
    }
    
    // ── SINGLE CELL режим ──────────────────────────────────────────
    // Показывает ОДНУ случайную клетку из тех, где радиация выше
    void DrawRadiationSingleCell(int2 heroPos)
    {
        if (_radiationConfigQuery.IsEmpty) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var buf    = _em.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        var config = _radiationConfigQuery.GetSingleton<ZoneRadiationConfig>();
        var grid   = _gridConfigQuery.GetSingleton<GridConfig>();

        // Получаем радиацию текущей клетки
        int currentRad = GetRadiationAt(heroPos, buf, grid.GridSize);
        
        // Центр показывает текущую радиацию
        _leftLabels[0].text = currentRad >= 0 ? currentRad.ToString() : "—";
        _leftHexes[0].style.backgroundColor = GetRadiationColor(currentRad, config);

        // Собираем все направления с более высокой радиацией
        var higherRadDirections = new System.Collections.Generic.List<int>();
        for (int i = 1; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int  rad = GetRadiationAt(pos, buf, grid.GridSize);

            if (rad > currentRad && rad >= 0)
            {
                higherRadDirections.Add(i);
            }
        }

        // Сбрасываем все лепестки в серый
        for (int i = 1; i < 7; i++)
        {
            _leftHexes[i].style.backgroundColor = ColorOff;
            _leftLabels[i].text = "";
        }

        // Если есть направления с высокой радиацией - показываем одно случайное
        if (higherRadDirections.Count > 0)
        {
            int randomIndex = higherRadDirections[UnityEngine.Random.Range(0, higherRadDirections.Count)];
            int2 pos = heroPos + HexOffsets[randomIndex];
            int  rad = GetRadiationAt(pos, buf, grid.GridSize);
            
            _leftLabels[randomIndex].text = rad.ToString();
            _leftHexes[randomIndex].style.backgroundColor = GetRadiationColor(rad, config);
        }
    }
    
    // ── ARC RAD режим ──────────────────────────────────────────────
    // Как SingleCell, но добавляет 2 случайных соседних лепестка
    void DrawRadiationArcRad(int2 heroPos)
    {
        if (_radiationConfigQuery.IsEmpty) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var buf    = _em.GetBuffer<ZoneCellRadiation>(mapEntity, true);
        var config = _radiationConfigQuery.GetSingleton<ZoneRadiationConfig>();
        var grid   = _gridConfigQuery.GetSingleton<GridConfig>();

        // Получаем радиацию текущей клетки
        int currentRad = GetRadiationAt(heroPos, buf, grid.GridSize);
        
        // Центр показывает текущую радиацию
        _leftLabels[0].text = currentRad >= 0 ? currentRad.ToString() : "—";
        _leftHexes[0].style.backgroundColor = GetRadiationColor(currentRad, config);

        // Собираем все направления с более высокой радиацией
        var higherRadDirections = new System.Collections.Generic.List<int>();
        for (int i = 1; i < 7; i++)
        {
            int2 pos = heroPos + HexOffsets[i];
            int  rad = GetRadiationAt(pos, buf, grid.GridSize);

            if (rad > currentRad && rad >= 0)
            {
                higherRadDirections.Add(i);
            }
        }

        // Сбрасываем все лепестки в серый
        for (int i = 1; i < 7; i++)
        {
            _leftHexes[i].style.backgroundColor = ColorOff;
            _leftLabels[i].text = "";
        }

        // Если есть направления с высокой радиацией
        if (higherRadDirections.Count > 0)
        {
            int mainDir = higherRadDirections[UnityEngine.Random.Range(0, higherRadDirections.Count)];
            int2 mainPos = heroPos + HexOffsets[mainDir];
            int  mainRad = GetRadiationAt(mainPos, buf, grid.GridSize);
            
            // Показываем основное направление
            _leftLabels[mainDir].text = mainRad.ToString();
            _leftHexes[mainDir].style.backgroundColor = GetRadiationColor(mainRad, config);
            
            // Добавляем 2 соседних лепестка (алгоритм как в ArcMode детектора)
            int left = ((mainDir - 2 + 6) % 6) + 1;
            int right = (mainDir % 6) + 1;

            // Рандом: 50/50 выбрать стиль подсветки
            if (UnityEngine.Random.value < 0.5f)
            {
                // вариант 1: подсвечиваем сразу слева и справа
                int2 leftPos = heroPos + HexOffsets[left];
                int2 rightPos = heroPos + HexOffsets[right];
                int leftRad = GetRadiationAt(leftPos, buf, grid.GridSize);
                int rightRad = GetRadiationAt(rightPos, buf, grid.GridSize);
                
                _leftHexes[left].style.backgroundColor = GetRadiationColor(leftRad, config);
                _leftHexes[right].style.backgroundColor = GetRadiationColor(rightRad, config);
            }
            else
            {
                // вариант 2: подсвечиваем две подряд идущие позиции с любой стороны
                int dir = UnityEngine.Random.value < 0.5f ? 1 : -1;
                int idx1 = (mainDir + dir - 1 + 6) % 6 + 1;
                int idx2 = (mainDir + 2 * dir - 1 + 6) % 6 + 1;

                int2 pos1 = heroPos + HexOffsets[idx1];
                int2 pos2 = heroPos + HexOffsets[idx2];
                int rad1 = GetRadiationAt(pos1, buf, grid.GridSize);
                int rad2 = GetRadiationAt(pos2, buf, grid.GridSize);
                
                _leftHexes[idx1].style.backgroundColor = GetRadiationColor(rad1, config);
                _leftHexes[idx2].style.backgroundColor = GetRadiationColor(rad2, config);
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  ПРАВЫЙ ЦВЕТОЧЕК — детектор
    // ══════════════════════════════════════════════════════════════
    void UpdateRightFlower(int2 heroPos)
    {
        switch (_mode)
        {
            case DetectorMode.Off:       DrawDetectorOff();                  break;
            case DetectorMode.MultiCell: DrawDetectorMultiCell(heroPos);    break;
            case DetectorMode.SingleCell: DrawDetectorSingleCell(heroPos);  break;
            case DetectorMode.ArcMode:   DrawDetectorArcMode(heroPos);      break;
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
        var grid   = _gridConfigQuery.GetSingleton<GridConfig>();

        // Центр — пусто
        _rightHexes[0].style.backgroundColor = ColorOff;
        _rightLabels[0].text = "";

        // Для каждого направления (1..6)
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
        var grid   = _gridConfigQuery.GetSingleton<GridConfig>();

        // 1. Ищем ближайшую аномалию во всех направлениях
        int nearestDist = -1;
        int nearestDir  = -1; // индекс направления 1..6

        for (int i = 1; i < 7; i++)
        {
            int dist = ScanDirectionWithPower(heroPos, HexOffsets[i], events, grid.GridSize);
            if (dist >= 0 && (nearestDist < 0 || dist < nearestDist))
            {
                nearestDist = dist;
                nearestDir  = i;
            }
        }

        // 2. Сбрасываем всё в серый
        for (int i = 0; i < 7; i++)
        {
            _rightHexes[i].style.backgroundColor = ColorOff;
            _rightLabels[i].text = "";
        }

        // Если ничего не найдено — конец
        if (nearestDir < 0)
        {
            _rightLabels[0].text = "—";
            return;
        }

        // 3. Центр показывает расстояние
        _rightLabels[0].text = nearestDist.ToString();
        _rightHexes[0].style.backgroundColor = GetAnomalyDistColor(nearestDist);

        // 4. Подсвечиваем точный лепесток
        _rightHexes[nearestDir].style.backgroundColor = GetAnomalyDistColor(nearestDist);
        //_rightLabels[nearestDir].text = nearestDist.ToString();
        /*
                // 5. Подсвечиваем 2 случайных соседа из оставшихся 5
                var neighbors = GetNeighborIndices(nearestDir);
                // neighbors[0..1] = соседи слева-справа
                // neighbors[2..4] = остальные

                // Генерируем 2 случайных индекса из 0..4 (5 соседей)
                int rand1 = UnityEngine.Random.Range(0, 5);
                int rand2 = UnityEngine.Random.Range(0, 5);
                if (rand2 == rand1) rand2 = (rand2 + 1) % 5;

                int idx1 = neighbors[rand1];
                int idx2 = neighbors[rand2];

                _rightHexes[idx1].style.backgroundColor = GetAnomalyDistColor(nearestDist);
                _rightHexes[idx2].style.backgroundColor = GetAnomalyDistColor(nearestDist);
        */
        // Индексы соседей 1..6 по кругу
        int left = ((nearestDir - 2 + 6) % 6) + 1;
        int right = (nearestDir % 6) + 1;

        // Рандом: 50/50 выбрать стиль подсветки
        if (UnityEngine.Random.value < 0.5f)
        {
            // вариант 1: подсвечиваем сразу слева и справа
            _rightHexes[left].style.backgroundColor = GetAnomalyDistColor(nearestDist);
            _rightHexes[right].style.backgroundColor = GetAnomalyDistColor(nearestDist);
        }
        else
        {
            // вариант 2: подсвечиваем две подряд идущие позиции с любой стороны
            // выбираем направление (1 = clockwise, -1 = counterclockwise)
            int dir = UnityEngine.Random.value < 0.5f ? 1 : -1;

            int idx1 = (nearestDir + dir - 1 + 6) % 6 + 1; // первый сосед
            int idx2 = (nearestDir + 2 * dir - 1 + 6) % 6 + 1; // следующий по кругу

            _rightHexes[idx1].style.backgroundColor = GetAnomalyDistColor(nearestDist);
            _rightHexes[idx2].style.backgroundColor = GetAnomalyDistColor(nearestDist);
        }

    }

    /// <summary>
    /// Возвращает массив из 5 индексов — соседи данного направления (кроме самого).
    /// [0,1] = непосредственные соседи (слева-справа по кругу)
    /// [2..4] = остальные
    /// </summary>
    int[] GetNeighborIndices(int dir)
    {
        // Все 6 направлений: 1,2,3,4,5,6 (по кругу)
        // Соседи слева-справа: dir-1, dir+1 (по модулю 6, со сдвигом на 1..6)
        int[] all = new int[5];
        int writeIdx = 0;

        int left  = ((dir - 2 + 6) % 6) + 1; // dir-1 в диапазоне 1..6
        int right = ((dir) % 6) + 1;         // dir+1 в диапазоне 1..6

        all[writeIdx++] = left;
        all[writeIdx++] = right;

        for (int i = 1; i <= 6; i++)
        {
            if (i == dir || i == left || i == right) continue;
            all[writeIdx++] = i;
        }

        return all;
    }

    // ══════════════════════════════════════════════════════════════
    //  СКАНИРОВАНИЕ ЛУЧА С УЧЁТОМ МОЩНОСТИ
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Сканируем луч в направлении dir с учётом мощности детектора.
    /// Если нашли аномалию на расстоянии d:
    ///   effectivePower = _power - d - anomaly.Visibility
    ///   если effectivePower >= 0 → возвращаем d
    ///   иначе → мощности не хватило → пропускаем
    /// Возвращаем расстояние до первой успешно обнаруженной аномалии или -1.
    /// </summary>
    int ScanDirectionWithPower(int2 heroPos, int2 dir, DynamicBuffer<ZoneEventElement> events, int2 gridSize)
    {
        // Луч идёт максимум на дистанцию min(_power, MAX_SCAN_RANGE)
        int maxDist = Mathf.Min(_power, MAX_SCAN_RANGE);

        for (int step = 1; step <= maxDist; step++)
        {
            int2 current = heroPos + dir * step;

            if (!HexGridUtils.IsHexInBounds(current, gridSize))
                return -1;

            // Ищем аномалию на этой клетке
            for (int e = 0; e < events.Length; e++)
            {
                var evt = events[e];
                if (evt.EventType != ZoneEventType.Anomaly) continue;
                //if (!evt.IsDiscovered) continue;
                if (evt.GridPos.x != current.x || evt.GridPos.y != current.y) continue;

                // Нашли аномалию на расстоянии step
                int effectivePower = _power - step - evt.Visibility;
                if (effectivePower >= 0)
                {
                    // Мощности хватает
                    return step;
                }
                // else: мощности не хватает — пропускаем эту аномалию, ищем дальше
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
    //  УТИЛИТЫ
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
                pos   = _em.GetComponentData<GridCoordinates>(entities[i]).Value;                
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
        if      (radiation <= cfg.LevelGreen)  c = new Color(cfg.ColorGreen.x,  cfg.ColorGreen.y,  cfg.ColorGreen.z);
        else if (radiation <= cfg.LevelYellow) c = new Color(cfg.ColorYellow.x, cfg.ColorYellow.y, cfg.ColorYellow.z);
        else if (radiation <= cfg.LevelOrange) c = new Color(cfg.ColorOrange.x, cfg.ColorOrange.y, cfg.ColorOrange.z);
        else                                   c = new Color(cfg.ColorRed.x,     cfg.ColorRed.y,     cfg.ColorRed.z);
        c.a = 0.8f;
        return c;
    }
    
    // ══════════════════════════════════════════════════════════════
    //  SINGLE CELL РЕЖИМ
    // ══════════════════════════════════════════════════════════════
    /// <summary>
    /// SingleCell режим - показывает только одну самую близкую аномалию с направлением и дальностью
    /// </summary>
    void DrawDetectorSingleCell(int2 heroPos)
    {
        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_em.HasBuffer<ZoneEventElement>(mapEntity))
        {
            DrawDetectorOff();
            return;
        }

        var events = _em.GetBuffer<ZoneEventElement>(mapEntity, true);
        var grid   = _gridConfigQuery.GetSingleton<GridConfig>();

        // 1. Ищем самую близкую аномалию во всех направлениях
        int nearestDist = -1;
        int nearestDir  = -1; // индекс направления 1..6

        for (int i = 1; i < 7; i++)
        {
            int dist = ScanDirectionWithPower(heroPos, HexOffsets[i], events, grid.GridSize);
            if (dist >= 0 && (nearestDist < 0 || dist < nearestDist))
            {
                nearestDist = dist;
                nearestDir  = i;
            }
        }

        // 2. Сбрасываем всё в серый
        for (int i = 0; i < 7; i++)
        {
            _rightHexes[i].style.backgroundColor = ColorOff;
            _rightLabels[i].text = "";
        }

        // Если ничего не найдено — конец
        if (nearestDir < 0)
        {
            _rightLabels[0].text = "—";
            return;
        }

        // 3. Центр показывает расстояние
        _rightLabels[0].text = nearestDist.ToString();
        _rightHexes[0].style.backgroundColor = GetAnomalyDistColor(nearestDist);

        // 4. Подсвечиваем ТОЛЬКО точный лепесток направления
        _rightHexes[nearestDir].style.backgroundColor = GetAnomalyDistColor(nearestDist);
    }
    
    // ══════════════════════════════════════════════════════════════
    //  УПРАВЛЕНИЕ АККУМУЛЯТОРОМ
    // ══════════════════════════════════════════════════════════════
    
    /// <summary>
    /// Списывает энергию с аккумулятора. Возвращает true если успешно, false если энергии не хватило
    /// </summary>
    bool ConsumeBattery(float amount)
    {
        if (amount <= 0) return true;
        
        var query = _em.CreateEntityQuery(typeof(BatteryData), typeof(ZoneModeTag));
        if (query.IsEmpty) return false;
        
        var entity = query.GetSingletonEntity();
        var battery = _em.GetComponentData<BatteryData>(entity);
        
        if (battery.CurrentCharge < amount)
        {
            // Недостаточно энергии
            return false;
        }
        
        battery.CurrentCharge -= amount;
        if (battery.CurrentCharge < 0) battery.CurrentCharge = 0;
        
        _em.SetComponentData(entity, battery);
        return true;
    }
    
    /// <summary>
    /// Списывает энергию за перемещение героя (1 энергия за клетку)
    /// </summary>
    void ConsumeBatteryForMovement()
    {
        ConsumeBattery(1f);
    }
    
    /// <summary>
    /// Обновляет UI прогресс-бара аккумулятора
    /// </summary>
    void UpdateBatteryUI()
    {
        var query = _em.CreateEntityQuery(typeof(BatteryData), typeof(ZoneModeTag));
        if (query.IsEmpty) return;
        
        var battery = query.GetSingleton<BatteryData>();
        
        float percentage = battery.CurrentCharge / battery.MaxCharge;
        percentage = Mathf.Clamp01(percentage);
        
        // Обновляем высоту заполнения
        _batteryFill.style.height = Length.Percent(percentage * 100f);
        
        // Обновляем текст
        _batteryLabel.text = $"{Mathf.RoundToInt(battery.CurrentCharge)}⚡";
        
        // Меняем цвет в зависимости от заряда
        Color fillColor;
        if (percentage > 0.5f)
            fillColor = new Color(0.4f, 0.8f, 1f, 0.9f); // Синий - полный
        else if (percentage > 0.25f)
            fillColor = new Color(1f, 0.8f, 0.2f, 0.9f); // Желтый - средний
        else
            fillColor = new Color(1f, 0.3f, 0.2f, 0.9f); // Красный - низкий
        
        _batteryFill.style.backgroundColor = fillColor;
    }
}
