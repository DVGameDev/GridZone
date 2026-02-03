using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class RadiationColorSystem : SystemBase
{
    protected override void OnUpdate()
    {
        if (!SystemAPI.HasSingleton<RadiationDebugState>())
            return;

        var debug = SystemAPI.GetSingletonRW<RadiationDebugState>();

        if (!debug.ValueRO.Dirty)
            return; // ⛔ ничего не делаем

        debug.ValueRW.Dirty = false; // 🔒 сбрасываем

        if (!SystemAPI.HasSingleton<GridMapTag>() ||
            !SystemAPI.HasSingleton<ZoneRadiationConfig>())
            return;

        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        if (!EntityManager.HasBuffer<ZoneCellRadiation>(mapEntity))
            return;

        var buffer = EntityManager.GetBuffer<ZoneCellRadiation>(mapEntity);
        var cfg = SystemAPI.GetSingleton<ZoneRadiationConfig>();

        var revealAll = debug.ValueRO.RevealAll;

        var colorLookup =
            GetComponentLookup<URPMaterialPropertyBaseColor>(false);
        var customLookup =
            GetComponentLookup<CellCustomColor>(false);
        var spawnerQuery = EntityManager.CreateEntityQuery(
    ComponentType.ReadOnly<ZoneSpawnerComponent>(),
    ComponentType.ReadOnly<ZoneBaseGridColor>());

        if (spawnerQuery.IsEmpty)
            return;

        var spawnerEntity = spawnerQuery.GetSingletonEntity();
        var baseGridColor =
            EntityManager.GetComponentData<ZoneBaseGridColor>(spawnerEntity);


        for (int i = 0; i < buffer.Length; i++)
        {
            var cell = buffer[i];

            bool showRadiation =
                revealAll || cell.IsVisited;

            float4 finalColor;

            if (showRadiation)
            {
                finalColor = cell.RadiationLevel switch
                {
                    0 => cfg.ColorGreen,
                    5 => cfg.ColorYellow,
                    10 => cfg.ColorOrange,
                    15 => cfg.ColorRed,
                    _ => cfg.ColorYellow
                };
            }
            else
            {
                // 🔥 ВОТ ОН — ОТКАТ
                finalColor = baseGridColor.Color;
            }

            if (colorLookup.HasComponent(cell.CellEntity))
                colorLookup[cell.CellEntity] =
                    new URPMaterialPropertyBaseColor { Value = finalColor };

            if (customLookup.HasComponent(cell.CellEntity))
                customLookup[cell.CellEntity] =
                    new CellCustomColor { BaseColor = finalColor };
        }

    }
}
