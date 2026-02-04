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

            // 🔥 ИСПРАВЛЕНО: Проверяем, изменилась ли позиция героя
            int2 currentPos = gridPos.ValueRO.Value;
            int2 lastPos = heroRadiation.ValueRO.LastProcessedPosition;
            
            // Начисляем радиацию только если герой переместился на новую клетку
            if (!currentPos.Equals(lastPos))
            {
                ProcessHeroCell(radiationBuffer, currentPos, ref heroRadiation.ValueRW);
                heroRadiation.ValueRW.LastProcessedPosition = currentPos;
            }
        }
    }

    private void ProcessHeroCell(DynamicBuffer<ZoneCellRadiation> radiationBuffer, int2 gridPos, ref HeroRadiationData heroRadiation)
    {
        var gridSize = SystemAPI.GetSingleton<GridConfig>().GridSize;
        int index = HexGridUtils.HexToIndex(gridPos, gridSize);

        if (index >= 0 && index < radiationBuffer.Length)
        {
            var cell = radiationBuffer[index];
           
            // 🔥 ИСПРАВЛЕНО: Считаем радиацию каждый ход, а не только при первом посещении
            heroRadiation.TotalRadiation += cell.RadiationLevel;
            
            bool wasVisited = cell.IsVisited;

            // Помечаем посещенной (если еще не посещали)
            if (!wasVisited)
            {
                radiationBuffer[index] = new ZoneCellRadiation
                {
                    GridPos = cell.GridPos,
                    CellEntity = cell.CellEntity,
                    RadiationLevel = cell.RadiationLevel,
                    IsVisited = true
                };

                // 🔥 Раскрасить клетку полностью (убрать прозрачность) только при первом посещении
                RevealCellColor(cell.CellEntity, cell.RadiationLevel);
            }

            Debug.Log($"[ZoneRadiation] Hero on cell {index}, radiation +{cell.RadiationLevel}. Total: {heroRadiation.TotalRadiation}");
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
