using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Визуализация событий на карте через простые цветные кубики
/// Добавьте этот компонент на любой GameObject в сцене
/// </summary>
public class ZoneEventVisualizer : MonoBehaviour
{
    [Header("Event Visualization Settings")]
    [Tooltip("Размер маркера события")]
    public float MarkerSize = 0.3f;
    
    [Tooltip("Высота маркера над гридом")]
    public float MarkerHeight = 1.0f;

    [Header("Event Colors")]
    public Color AnomalyColor = new Color(1f, 0f, 1f, 0.8f);      // Фиолетовый
    public Color FightColor = new Color(1f, 0.3f, 0f, 0.8f);      // Оранжевый
    public Color EventColor = new Color(0f, 1f, 1f, 0.8f);        // Голубой
    
    [Header("Alpha Settings")]
    public float DiscoveredAlpha = 1.0f;
    public float UndiscoveredAlpha = 0.3f;

    private EntityManager _entityManager;
    private Dictionary<Entity, GameObject> _eventMarkers = new Dictionary<Entity, GameObject>();
    private Material _markerMaterial;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        
        // Создаём материал для маркеров
        _markerMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        _markerMaterial.SetFloat("_Surface", 1); // Transparent
    }

    void Update()
    {
        if (_entityManager == default)
            return;

        // Проверяем ZONE режим
        var zoneQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<ZoneModeTag>());
        if (zoneQuery.IsEmpty)
        {
            CleanupMarkers();
            return;
        }

        // Получаем карту
        var mapQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridMapTag>());
        if (mapQuery.IsEmpty) return;

        var mapEntity = mapQuery.GetSingletonEntity();
        if (!_entityManager.HasBuffer<ZoneEventElement>(mapEntity)) return;

        var eventBuffer = _entityManager.GetBuffer<ZoneEventElement>(mapEntity, true);
        var gridConfigQuery = _entityManager.CreateEntityQuery(ComponentType.ReadOnly<GridConfig>());
        if (gridConfigQuery.IsEmpty) return;
        
        var gridConfig = gridConfigQuery.GetSingleton<GridConfig>();

        // Обновляем визуализацию
        UpdateEventMarkers(eventBuffer, gridConfig);
    }

    void UpdateEventMarkers(DynamicBuffer<ZoneEventElement> eventBuffer, GridConfig gridConfig)
    {
        HashSet<Entity> activeEvents = new HashSet<Entity>();

        foreach (var eventElement in eventBuffer)
        {
            if (!_entityManager.Exists(eventElement.EventEntity)) continue;

            activeEvents.Add(eventElement.EventEntity);

            // Создаём маркер, если его еще нет
            if (!_eventMarkers.ContainsKey(eventElement.EventEntity))
            {
                CreateMarker(eventElement, gridConfig);
            }

            // Обновляем маркер
            UpdateMarker(eventElement);
        }

        // Удаляем маркеры для несуществующих событий
        var toRemove = new List<Entity>();
        foreach (var kvp in _eventMarkers)
        {
            if (!activeEvents.Contains(kvp.Key))
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
                toRemove.Add(kvp.Key);
            }
        }
        foreach (var entity in toRemove)
        {
            _eventMarkers.Remove(entity);
        }
    }

    void CreateMarker(ZoneEventElement eventElement, GridConfig gridConfig)
    {
        // Создаём простой куб как маркер
        GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
        marker.name = $"EventMarker_{eventElement.EventType}_{eventElement.GridPos}";
        
        // Позиция
        float3 worldPos = HexGridUtils.HexAxialToWorld(eventElement.GridPos, gridConfig.Spacing);
        worldPos.y = MarkerHeight;
        marker.transform.position = worldPos;
        marker.transform.localScale = Vector3.one * MarkerSize;

        // Материал
        var renderer = marker.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.material = new Material(_markerMaterial);

            // Цвет по типу события
            Color color = GetEventColor(eventElement.EventType);
            color.a = eventElement.IsDiscovered ? DiscoveredAlpha : UndiscoveredAlpha;
            renderer.material.color = color;
        }

        // Сохраняем
        _eventMarkers[eventElement.EventEntity] = marker;
    }

    void UpdateMarker(ZoneEventElement eventElement)
    {
        if (!_eventMarkers.TryGetValue(eventElement.EventEntity, out var marker))
            return;

        if (marker == null) return;

        // Обновляем прозрачность при обнаружении
        var renderer = marker.GetComponent<MeshRenderer>();
        if (renderer != null && renderer.material != null)
        {
            Color color = renderer.material.color;
            float targetAlpha = eventElement.IsDiscovered ? DiscoveredAlpha : UndiscoveredAlpha;
            
            if (Mathf.Abs(color.a - targetAlpha) > 0.01f)
            {
                color.a = Mathf.Lerp(color.a, targetAlpha, Time.deltaTime * 5f);
                renderer.material.color = color;
            }
        }
    }

    Color GetEventColor(ZoneEventType eventType)
    {
        switch (eventType)
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
        {
            if (marker != null)
                Destroy(marker);
        }
        _eventMarkers.Clear();
    }

    void OnDestroy()
    {
        CleanupMarkers();
    }
}
