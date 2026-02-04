using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

public class ZoneEventVisualizer : MonoBehaviour
{
    [Header("Event Visualization Settings")]
    public float IconSize = 1.0f;
    public float IconHeight = 1.5f;

    [Header("Alpha Settings")]
    public float DiscoveredAlpha = 1.0f;
    public float UndiscoveredAlpha = 0.3f;

    private EntityManager _entityManager;
    private Dictionary<Entity, GameObject> _eventMarkers = new Dictionary<Entity, GameObject>();
    
    // Спрайты для разных типов событий
    private Sprite _questIcon;
    private Sprite _battleIcon;
    private Sprite _anomalyIcon;

    // Кэшированные запросы
    private EntityQuery _zoneQuery;
    private EntityQuery _mapQuery;
    private EntityQuery _gridConfigQuery;
    private EntityQuery _debugQuery;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        _zoneQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ZoneModeTag>());
        _mapQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridMapTag>());
        _gridConfigQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridConfig>());
        _debugQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<EventDebugState>());

        // Загружаем иконки из Resources/Icons
        _questIcon = Resources.Load<Sprite>("Icons/Quest_icon");
        _battleIcon = Resources.Load<Sprite>("Icons/Battle_icon");
        _anomalyIcon = Resources.Load<Sprite>("Icons/Anomaly_icon");
        
        if (_questIcon == null) Debug.LogWarning("[ZoneEventVisualizer] Quest_icon not found in Resources/Icons!");
        if (_battleIcon == null) Debug.LogWarning("[ZoneEventVisualizer] Battle_icon not found in Resources/Icons!");
        if (_anomalyIcon == null) Debug.LogWarning("[ZoneEventVisualizer] Anomaly_icon not found in Resources/Icons!");
    }

    void Update()
    {
        if (_zoneQuery.IsEmpty)
        {
            CleanupMarkers();
            return;
        }
        if (_mapQuery.IsEmpty || _gridConfigQuery.IsEmpty) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_entityManager.HasBuffer<ZoneEventElement>(mapEntity)) return;

        var eventBuffer = _entityManager.GetBuffer<ZoneEventElement>(mapEntity, true);
        var gridConfig = _gridConfigQuery.GetSingleton<GridConfig>();

        UpdateEventMarkers(eventBuffer, gridConfig);
    }

    void UpdateEventMarkers(DynamicBuffer<ZoneEventElement> eventBuffer, GridConfig gridConfig)
    {
        var activeEvents = new HashSet<Entity>();
        
        // Проверяем режим дебага "показать все события"
        bool showAllEvents = false;
        if (!_debugQuery.IsEmpty)
        {
            var debugState = _debugQuery.GetSingleton<EventDebugState>();
            showAllEvents = debugState.ShowAll;
        }

        foreach (var evt in eventBuffer)
        {
            if (!_entityManager.Exists(evt.EventEntity)) continue;
            activeEvents.Add(evt.EventEntity);

            // Показываем только открытые события, либо все в режиме дебага
            bool shouldShow = evt.IsDiscovered || showAllEvents;
            
            if (!_eventMarkers.ContainsKey(evt.EventEntity))
            {
                if (shouldShow)
                    CreateMarker(evt, gridConfig);
            }
            else
            {
                if (shouldShow)
                    UpdateMarker(evt, showAllEvents);
                else
                {
                    // Скрываем маркер, если режим дебага выключен и событие не открыто
                    if (_eventMarkers[evt.EventEntity] != null)
                        Destroy(_eventMarkers[evt.EventEntity]);
                    _eventMarkers.Remove(evt.EventEntity);
                }
            }
        }

        // Удаляем маркеры событий, которых больше нет
        var toRemove = new List<Entity>();
        foreach (var kvp in _eventMarkers)
        {
            if (!activeEvents.Contains(kvp.Key))
            {
                if (kvp.Value != null) Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var e in toRemove) _eventMarkers.Remove(e);
    }

    void CreateMarker(ZoneEventElement evt, GridConfig gridConfig)
    {
        var marker = new GameObject($"EventMarker_{evt.EventType}_{evt.GridPos}");

        float3 worldPos = HexGridUtils.HexAxialToWorld(evt.GridPos, gridConfig.Spacing);
        worldPos.y = IconHeight;
        marker.transform.position = worldPos;

        // Создаём SpriteRenderer для отображения иконки
        var spriteRenderer = marker.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetEventSprite(evt.EventType);
        
        // Настраиваем размер и поворот
        marker.transform.localScale = Vector3.one * IconSize;
        marker.transform.rotation = Quaternion.Euler(90, 0, 0); // Поворачиваем чтобы было видно сверху

        // Устанавливаем прозрачность
        Color color = Color.white;
        color.a = evt.IsDiscovered ? DiscoveredAlpha : UndiscoveredAlpha;
        spriteRenderer.color = color;

        _eventMarkers[evt.EventEntity] = marker;
    }

    void UpdateMarker(ZoneEventElement evt, bool showAllEvents)
    {
        if (!_eventMarkers.TryGetValue(evt.EventEntity, out var marker) || marker == null) return;

        var spriteRenderer = marker.GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        // В режиме дебага "показать все" - показываем все события с полной прозрачностью
        float targetAlpha = showAllEvents ? 1.0f : (evt.IsDiscovered ? DiscoveredAlpha : UndiscoveredAlpha);
        Color color = spriteRenderer.color;

        if (Mathf.Abs(color.a - targetAlpha) > 0.01f)
        {
            color.a = Mathf.Lerp(color.a, targetAlpha, Time.deltaTime * 5f);
            spriteRenderer.color = color;
        }
    }

    Sprite GetEventSprite(ZoneEventType type)
    {
        switch (type)
        {
            case ZoneEventType.Anomaly: return _anomalyIcon;
            case ZoneEventType.Fight: return _battleIcon;
            case ZoneEventType.Event: return _questIcon;
            default: return null;
        }
    }

    void CleanupMarkers()
    {
        foreach (var marker in _eventMarkers.Values)
            if (marker != null) Destroy(marker);
        _eventMarkers.Clear();
    }

    void OnDestroy() { CleanupMarkers(); }
}