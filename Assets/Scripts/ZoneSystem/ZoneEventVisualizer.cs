using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

public class ZoneEventVisualizer : MonoBehaviour
{
    [Header("Event Visualization Settings")]
    public float MarkerSize = 0.3f;
    public float MarkerHeight = 1.0f;

    [Header("Event Colors")]
    public Color AnomalyColor = new Color(1f, 0f, 1f, 0.8f);
    public Color FightColor = new Color(1f, 0.3f, 0f, 0.8f);
    public Color EventColor = new Color(0f, 1f, 1f, 0.8f);

    [Header("Alpha Settings")]
    public float DiscoveredAlpha = 1.0f;
    public float UndiscoveredAlpha = 0.3f;

    private EntityManager _entityManager;
    private Dictionary<Entity, GameObject> _eventMarkers = new Dictionary<Entity, GameObject>();
    private Material _markerMaterial;

    // Кэшированные запросы
    private EntityQuery _zoneQuery;
    private EntityQuery _mapQuery;
    private EntityQuery _gridConfigQuery;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

        _zoneQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ZoneModeTag>());
        _mapQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridMapTag>());
        _gridConfigQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridConfig>());

        _markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _markerMaterial.SetFloat("_Surface", 1);
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

        foreach (var evt in eventBuffer)
        {
            if (!_entityManager.Exists(evt.EventEntity)) continue;
            activeEvents.Add(evt.EventEntity);

            if (!_eventMarkers.ContainsKey(evt.EventEntity))
                CreateMarker(evt, gridConfig);

            UpdateMarker(evt);
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
        var marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = $"EventMarker_{evt.EventType}_{evt.GridPos}";

        float3 worldPos = HexGridUtils.HexAxialToWorld(evt.GridPos, gridConfig.Spacing);
        worldPos.y = MarkerHeight;
        marker.transform.position = worldPos;
        marker.transform.localScale = Vector3.one * MarkerSize;

        var renderer = marker.GetComponent<MeshRenderer>();
        renderer.material = new Material(_markerMaterial);

        Color color = GetEventColor(evt.EventType);
        color.a = evt.IsDiscovered ? DiscoveredAlpha : UndiscoveredAlpha;
        renderer.material.color = color;

        _eventMarkers[evt.EventEntity] = marker;
    }

    void UpdateMarker(ZoneEventElement evt)
    {
        if (!_eventMarkers.TryGetValue(evt.EventEntity, out var marker) || marker == null) return;

        var renderer = marker.GetComponent<MeshRenderer>();
        if (renderer == null || renderer.material == null) return;

        float targetAlpha = evt.IsDiscovered ? DiscoveredAlpha : UndiscoveredAlpha;
        Color color = renderer.material.color;

        if (Mathf.Abs(color.a - targetAlpha) > 0.01f)
        {
            color.a = Mathf.Lerp(color.a, targetAlpha, Time.deltaTime * 5f);
            renderer.material.color = color;
        }
    }

    Color GetEventColor(ZoneEventType type)
    {
        switch (type)
        {
            case ZoneEventType.Anomaly: return AnomalyColor;
            case ZoneEventType.Fight: return FightColor;
            case ZoneEventType.Event: return EventColor;
            default: return Color.white;
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