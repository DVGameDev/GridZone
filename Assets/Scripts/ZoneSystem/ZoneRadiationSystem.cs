using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

/// <summary>
/// Обработка радиации при движении героя в ZONE режиме
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitMoveSystem))]
public partial class ZoneRadiationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Проверяем ZONE режим
        if (!SystemAPI.HasSingleton<ZoneModeTag>()) return;

        // Проверяем существование GridMapTag
        if (!SystemAPI.HasSingleton<GridMapTag>()) return;

        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        if (!EntityManager.HasBuffer<ZoneCellRadiation>(mapEntity)) return;

        var radiationBuffer = EntityManager.GetBuffer<ZoneCellRadiation>(mapEntity);

        // Обрабатываем героя (ID = 0)
        foreach (var (heroRadiation, gridPos, unitId) in
                 SystemAPI.Query<RefRW<HeroRadiationData>, RefRO<GridCoordinates>, RefRO<UnitIdComponent>>())
        {
            if (unitId.ValueRO.UnitId != 0) continue;

            ProcessHeroCell(radiationBuffer, gridPos.ValueRO.Value, ref heroRadiation.ValueRW);
        }
    }

    private void ProcessHeroCell(DynamicBuffer<ZoneCellRadiation> radiationBuffer, int2 gridPos, ref HeroRadiationData heroRadiation)
    {
        var gridSize = SystemAPI.GetSingleton<GridConfig>().GridSize;
        int index = HexGridUtils.HexToIndex(gridPos, gridSize);

        if (index >= 0 && index < radiationBuffer.Length)
        {
            var cell = radiationBuffer[index];

            if (!cell.IsVisited)
            {
                // 🔥 Первое посещение!
                heroRadiation.TotalRadiation += cell.RadiationLevel;

                // Помечаем посещенной
                radiationBuffer[index] = new ZoneCellRadiation
                {
                    GridPos = cell.GridPos,
                    CellEntity = cell.CellEntity,
                    RadiationLevel = cell.RadiationLevel,
                    IsVisited = true
                };

                Debug.Log($"[ZoneRadiation] Hero visited cell {index}, radiation +{cell.RadiationLevel}. Total: {heroRadiation.TotalRadiation}");

                // 🔥 Раскрасить клетку полностью (убрать прозрачность)
                RevealCellColor(cell.CellEntity, cell.RadiationLevel);

            }
        }
    }

    private void RevealCellColor(Entity cellEntity, int radiationLevel)
    {
        // Получаем конфиг радиации
        var radiationConfig = SystemAPI.GetSingleton<ZoneRadiationConfig>();

        // Определяем цвет по уровню радиации (почти прозрачный)
        float4 cellColor;
        switch (radiationLevel)
        {
            case 0: cellColor = radiationConfig.ColorGreen; break;
            case 5: cellColor = radiationConfig.ColorYellow; break;
            case 10: cellColor = radiationConfig.ColorOrange; break;
            case 15: cellColor = radiationConfig.ColorRed; break;
            default: cellColor = radiationConfig.ColorYellow; break;
        }

        // 🔥 Применяем цвет радиации (почти прозрачный, чтобы видеть карту)
        if (EntityManager.HasComponent<URPMaterialPropertyBaseColor>(cellEntity))
        {
            EntityManager.SetComponentData(cellEntity, new URPMaterialPropertyBaseColor { Value = cellColor });
        }

        if (EntityManager.HasComponent<CellCustomColor>(cellEntity))
        {
            EntityManager.SetComponentData(cellEntity, new CellCustomColor { BaseColor = cellColor });
        }
    }

}
