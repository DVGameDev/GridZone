using NorskaLibExamples.Spreadsheets;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
[UpdateAfter(typeof(UnitSpawnerSystem))]
public partial class EffectBootstrapSystem : SystemBase
{
    private bool _hasSpawned = false;

    protected override void OnCreate()
    {
        RequireForUpdate<UnitIdComponent>(); // Ждем, пока появится хотя бы один юнит с ID
        RequireForUpdate<GridConfig>();      // Ждем конфиг сетки
    }

    protected override void OnUpdate()
    {
        if (_hasSpawned) return;

        var container = Resources.Load<SpreadsheetContainer>("SpreadsheetContainer");
        if (container == null || container.Content == null || container.Content.Effects == null)
        {
            return;
        }

        // 1. Получаем всех юнитов. Если их нет — ждем следующего кадра.
        var unitQuery = SystemAPI.QueryBuilder().WithAll<UnitIdComponent>().Build();
        if (unitQuery.IsEmpty) return;

        var unitEntities = unitQuery.ToEntityArray(Allocator.Temp);
        var unitIds = unitQuery.ToComponentDataArray<UnitIdComponent>(Allocator.Temp);
        var unitMap = new NativeHashMap<int, Entity>(unitIds.Length, Allocator.Temp);

        for (int i = 0; i < unitIds.Length; i++)
        {
            if (!unitMap.ContainsKey(unitIds[i].UnitId))
                unitMap.Add(unitIds[i].UnitId, unitEntities[i]);
        }

        // 2. Спавн Эффектов
        foreach (var cfg in container.Content.Effects)
        {
            Entity effectEntity = EntityManager.CreateEntity();

            // A. Статы
            EntityManager.AddComponentData(effectEntity, new EffectStatComponent
            {
                Name = new FixedString64Bytes(cfg.EffectType.ToString()),
                StatusType = cfg.StatusType,
                Power = cfg.Power,
                Repeat = cfg.Repeat,
                Duration = cfg.Duration,
                Charges = cfg.Charges,
                TargetType = cfg.TargetType,
                Description = new FixedString128Bytes(cfg.Description ?? ""),
                IsVisible = cfg.IsVisible
            });

            // B. Формы (Вешаем прямо на ЭФФЕКТ)
            EntityManager.AddComponentData(effectEntity, new EffectShapeData
            {
                AimShape = new AimShapeConfig
                {
                    Type = cfg.AimShapeType,
                    SizeX = cfg.ASizeX,
                    SizeZ = cfg.ASizeZ,
                    Offset = cfg.Offset
                },
                EffectShape = new EffectShapeConfig
                {
                    Type = cfg.EffectShapeType,
                    SizeX = cfg.ESizeX,
                    SizeZ = cfg.ESizeZ,
                    EffectLevel = cfg.EffectShapeLevel
                },
                
            });

            // C. Привязка к Юниту (Только ссылка)
            // Ищем юнита по ID из конфига эффекта
            if (unitMap.TryGetValue(cfg.UnitID, out Entity unitEntity))
            {
                // Добавляем компонент-ссылку на юнита
                EntityManager.AddComponentData(unitEntity, new UnitEffectData
                {
                    EffectEntity = effectEntity
                });
                // Debug.Log($"Effect '{cfg.EffectType}' linked to Unit ID {cfg.UnitID}");
            }
        }

        unitEntities.Dispose();
        unitIds.Dispose();
        unitMap.Dispose();

        _hasSpawned = true;
    }
}
