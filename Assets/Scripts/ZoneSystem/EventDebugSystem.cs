using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система инициализации состояний дебага (радиация и события)
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(ZoneSpawnerSystem))]
public partial struct DebugStateInitSystem : ISystem
{
    private bool _initialized;
    
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ZoneModeTag>();
    }

    public void OnUpdate(ref SystemState state)
    {
        if (_initialized) return;
        
        // Создаём синглтон для дебага радиации
        var radiationQuery = SystemAPI.QueryBuilder().WithAll<RadiationDebugState>().Build();
        if (radiationQuery.IsEmpty)
        {
            var radiationDebugEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(radiationDebugEntity, "RadiationDebugState");
            state.EntityManager.AddComponentData(radiationDebugEntity, new RadiationDebugState
            {
                RevealAll = false,
                Dirty = false
            });
            Debug.Log("[DebugStateInit] RadiationDebugState singleton created");
        }
        
        // Создаём синглтон для дебага событий
        var eventQuery = SystemAPI.QueryBuilder().WithAll<EventDebugState>().Build();
        if (eventQuery.IsEmpty)
        {
            var eventDebugEntity = state.EntityManager.CreateEntity();
            state.EntityManager.SetName(eventDebugEntity, "EventDebugState");
            state.EntityManager.AddComponentData(eventDebugEntity, new EventDebugState
            {
                ShowAll = false
            });
            Debug.Log("[DebugStateInit] EventDebugState singleton created");
        }
        
        _initialized = true;
    }
}
