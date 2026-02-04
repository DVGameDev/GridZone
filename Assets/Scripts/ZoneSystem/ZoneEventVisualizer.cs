using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

public class ZoneEventVisualizer : MonoBehaviour
{
    //[Header("Event Icons Config")]
    //public EventIconsConfig IconsConfig;

    [Header("Event Visualization Settings")]
    public float IconSize = 1.0f;
    public float IconHeight = 1.5f;

    [Header("Alpha Settings")]
    public float DiscoveredAlpha = 1.0f;
    public float UndiscoveredAlpha = 0.3f;

    [Header("Rendering Settings")]
    public string SortingLayerName = "Default";
    public int SortingOrder = 100;

    private EntityManager _entityManager;
    private Dictionary<Entity, GameObject> _eventMarkers = new Dictionary<Entity, GameObject>();
    [Header("Icons")]
    public Sprite _questIcon;
    public Sprite _battleIcon;
    public Sprite _anomalyIcon;

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

        
    }

    void Update()
    {
        if (_zoneQuery.IsEmpty)
        {
            CleanupMarkers();
            return;
        }

        if (_debugQuery.IsEmpty) return;

        // 🔥 Проверяем флаг Dirty - обновляем только когда нужно
        var debugEntity = _debugQuery.GetSingletonEntity();
        var debugState = _entityManager.GetComponentData<EventDebugState>(debugEntity);

        if (!debugState.Dirty) return; // ⛔ ничего не делаем

        // 🔒 Сбрасываем флаг
        debugState.Dirty = false;
        _entityManager.SetComponentData(debugEntity, debugState);

        if (_mapQuery.IsEmpty || _gridConfigQuery.IsEmpty) return;

        var mapEntity = _mapQuery.GetSingletonEntity();
        if (!_entityManager.HasBuffer<ZoneEventElement>(mapEntity)) return;

        var eventBuffer = _entityManager.GetBuffer<ZoneEventElement>(mapEntity, true);
        var gridConfig = _gridConfigQuery.GetSingleton<GridConfig>();

        UpdateEventMarkers(eventBuffer, gridConfig, debugState.ShowAll);
    }

    void UpdateEventMarkers(DynamicBuffer<ZoneEventElement> eventBuffer, GridConfig gridConfig, bool showAllEvents)
    {
        var activeEvents = new HashSet<Entity>();

        Debug.Log($"[ZoneEventVisualizer] Update triggered: {eventBuffer.Length} events, showAll={showAllEvents}");

        foreach (var evt in eventBuffer)
        {
            if (!_entityManager.Exists(evt.EventEntity)) continue;

            activeEvents.Add(evt.EventEntity);
            bool shouldShow = evt.IsDiscovered || showAllEvents;

            if (!_eventMarkers.ContainsKey(evt.EventEntity))
            {
                if (shouldShow)
                {
                    Debug.Log($"[ZoneEventVisualizer] Creating marker for {evt.EventType} at {evt.GridPos}");
                    CreateMarker(evt, gridConfig);
                }
            }
            else
            {
                if (shouldShow)
                    UpdateMarker(evt, showAllEvents);
                else
                {
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

        var spriteRenderer = marker.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = GetEventSprite(evt.EventType);
        spriteRenderer.sortingLayerName = SortingLayerName;
        spriteRenderer.sortingOrder = SortingOrder;
        spriteRenderer.material = new Material(Shader.Find("Sprites/Default"));

        marker.transform.localScale = Vector3.one * IconSize;
        marker.transform.rotation = Quaternion.Euler(90, 0, 0);

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

        float targetAlpha = showAllEvents ? 1.0f : (evt.IsDiscovered ? DiscoveredAlpha : UndiscoveredAlpha);
        Color color = spriteRenderer.color;
        color.a = targetAlpha;
        spriteRenderer.color = color;
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
