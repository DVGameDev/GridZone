using Unity.Entities;
using UnityEngine;

/// <summary>
/// В Zone режиме при старте автоматически выбирает юнита 0 и ставит режим Move.
/// Отключается после первого успешного выбора.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(SelectionSystem))]
public partial class ZoneAutoSelectSystem : SystemBase
{
    protected override void OnCreate()
    {
        RequireForUpdate<ZoneModeTag>();
    }

    protected override void OnUpdate()
    {
        // Ждём, пока ActiveUnitComponent синглтон существует
        if (!SystemAPI.HasSingleton<ActiveUnitComponent>()) return;

        var state = SystemAPI.GetSingleton<ActiveUnitComponent>();

        // Если юнит уже выбран — мы уже сделали работу, выключаемся
        if (state.Unit != Entity.Null)
        {
            Enabled = false;
            return;
        }

        // Ищем юнит с UnitId == 0
        foreach (var (unitId, entity) in SystemAPI.Query<RefRO<UnitIdComponent>>().WithEntityAccess())
        {
            if (unitId.ValueRO.UnitId != 0) continue;

            // Нашли hero — выбираем и ставим Move
            state.Unit = entity;
            state.Mode = InteractionMode.Move;
            SystemAPI.SetSingleton(state);

            Debug.Log($"[ZoneAutoSelect] Hero (UnitId=0) auto-selected, Mode=Move");
            Enabled = false;
            return;
        }
    }
}