using NorskaLibExamples.Spreadsheets;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class UnitSpawnerSystem : SystemBase
{
    private bool _hasSpawned = false;

    protected override void OnCreate()
    {
        RequireForUpdate<GridConfig>();
        RequireForUpdate<GridMapTag>();
        RequireForUpdate<UnitPrefabElement>(); // Ждем буфер
    }

    protected override void OnUpdate()
    {
        if (_hasSpawned) return;

        // 1. Загрузка Spreadsheet
        var container = Resources.Load<SpreadsheetContainer>("SpreadsheetContainer");
        if (container == null || container.Content == null || container.Content.Units == null)
        {
            Debug.LogError("[UnitSpawnerSystem] Failed to load SpreadsheetContainer!");
            return;
        }

        // 2. Сбор префабов из ECS (PrefabMapAuthoring)
        if (!SystemAPI.TryGetSingletonEntity<UnitPrefabElement>(out Entity prefabMapEntity))
        {
            Debug.LogError("[UnitSpawnerSystem] Singleton with UnitPrefabElement buffer not found!");
            return;
        }

        DynamicBuffer<UnitPrefabElement> prefabBuffer = EntityManager.GetBuffer<UnitPrefabElement>(prefabMapEntity);
        NativeHashMap<FixedString64Bytes, Entity> prefabMap = new NativeHashMap<FixedString64Bytes, Entity>(prefabBuffer.Length, Allocator.Temp);

        for (int i = 0; i < prefabBuffer.Length; i++)
        {
            var element = prefabBuffer[i];
            if (!prefabMap.ContainsKey(element.UnitName))
                prefabMap.Add(element.UnitName, element.PrefabEntity);
        }

        // 3. Подготовка
        var gridConfig = SystemAPI.GetSingleton<GridConfig>();
        int2 gridSize = SystemAPI.GetSingleton<GridMapTag>().Size;
        List<(int index, Entity unit, UnitLayer layer)> mapUpdates = new List<(int, Entity, UnitLayer)>();

        Debug.Log($"[UnitSpawnerSystem]: Spawning {container.Content.Units.Count} units...");

        // 4. Спаун
        foreach (var unitCfg in container.Content.Units)
        {
            if (string.IsNullOrEmpty(unitCfg.UnitPrefab)) continue;

            FixedString64Bytes prefabName = new FixedString64Bytes(unitCfg.UnitPrefab);

            if (prefabMap.TryGetValue(prefabName, out Entity prefabEntity))
            {
                Entity unitInstance = EntityManager.Instantiate(prefabEntity);

                // --- A. СЛОЙ (Layer) ---
                UnitLayer layer = UnitLayer.Ground;
                if (!string.IsNullOrEmpty(unitCfg.Layer))
                {
                    string l = unitCfg.Layer.ToLower();
                    if (l.Contains("sky")) layer = UnitLayer.Sky;
                    else if (l.Contains("under")) layer = UnitLayer.Underground;
                }

                // --- B. ВЫСОТА (Height) ---
                float posY = gridConfig.HeightGround;
                switch (layer)
                {
                    case UnitLayer.Sky: posY = gridConfig.HeightSky; break;
                    case UnitLayer.Underground: posY = gridConfig.HeightUnderground; break;
                    case UnitLayer.Ground: posY = gridConfig.HeightGround; break;
                }

                // --- C. ПОЗИЦИЯ + D. ПОВОРОТ (С УЧЕТОМ LAYOUT) ---
                float spacing = gridConfig.Spacing;
                float3 finalPos;
                quaternion rotation = quaternion.identity;
                int2 facingDir = new int2(unitCfg.FacingX, unitCfg.FacingZ);

                if (gridConfig.Layout == GridLayoutType.HexFlatTop)
                {
                    // 🔥 HEX: используем Axial координаты
                    int2 hexCoords = new int2(unitCfg.PositionX, unitCfg.PositionZ);
                    finalPos = HexGridUtils.GetHexWorldPosition(hexCoords, spacing, layer, gridConfig);

                    // Facing для Hex
                    if (facingDir.Equals(int2.zero))
                        facingDir = new int2(0, 1); // Default для hex

                    float3 hexLookDir = new float3(facingDir.x, 0, facingDir.y);
                    if (math.lengthsq(hexLookDir) > 0.001f)
                        rotation = quaternion.LookRotation(hexLookDir, math.up());
                }
                else
                {
                    // 🔥 QUAD: оригинальная логика с центрированием
                    float offsetX = (unitCfg.SizeX - 1) * spacing * 0.5f;
                    float offsetZ = -(unitCfg.SizeZ - 1) * spacing * 0.5f;
                    float3 basePos = new float3(unitCfg.PositionX * spacing, posY, unitCfg.PositionZ * spacing);
                    finalPos = new float3(basePos.x + offsetX, basePos.y, basePos.z + offsetZ);

                    // Facing для Quad
                    if (facingDir.Equals(int2.zero))
                        facingDir = new int2(0, -1);

                    float3 quadLookDir = new float3(facingDir.x, 0, facingDir.y);
                    if (math.lengthsq(quadLookDir) > 0.001f)
                        rotation = quaternion.LookRotation(quadLookDir, math.up());
                }

                // Применяем Transform
                EntityManager.SetComponentData(unitInstance, LocalTransform.FromPositionRotation(finalPos, rotation));

                // 🔥 DEBUG
                Debug.Log($"[Spawn] Unit {unitCfg.Id}: GridCoords=({unitCfg.PositionX},{unitCfg.PositionZ}) WorldPos={finalPos} Layout={gridConfig.Layout}");


                // --- COMPONENTS ---


                // --- COMPONENTS ---
                EntityManager.AddComponentData(unitInstance, new UnitIdComponent { UnitId = unitCfg.Id });
                EntityManager.AddComponentData(unitInstance, new UnitStats
                {
                    Strength = unitCfg.Strength,
                    MoveRange = unitCfg.BaseRenewAP
                });
                EntityManager.AddComponentData(unitInstance, new AnimationSync { WalkCycleLength = 1.2f });
                EntityManager.AddComponentData(unitInstance, new UnitSize { Value = new int2(unitCfg.SizeX, unitCfg.SizeZ) });
                EntityManager.AddComponentData(unitInstance, new UnitLayerData { Value = layer });
                EntityManager.AddComponentData(unitInstance, new UnitFacing { Value = facingDir });
                EntityManager.AddComponentData(unitInstance, new MoveCommand { IsMoving = false, MoveSpeed = 5.0f });
                EntityManager.AddComponentData(unitInstance, new GridCoordinates { Value = new int2(unitCfg.PositionX, unitCfg.PositionZ) });

                // --- MAP REGISTRATION ---
                if (unitCfg.PositionX >= 0 && unitCfg.PositionX < gridSize.x &&
                    unitCfg.PositionZ >= 0 && unitCfg.PositionZ < gridSize.y)
                {
                    // Заполняем ВСЕ клетки, которые занимает юнит
                    for (int x = 0; x < unitCfg.SizeX; x++)
                    {
                        for (int y = 0; y < unitCfg.SizeZ; y++)
                        {
                            int tx = unitCfg.PositionX + x;
                            int ty = unitCfg.PositionZ - y; // Вниз по Z

                            if (tx >= 0 && tx < gridSize.x && ty >= 0 && ty < gridSize.y)
                            {
                                int index = tx * gridSize.y + ty;
                                mapUpdates.Add((index, unitInstance, layer));
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogError($"[UnitSpawnerSystem] Unit out of bounds: {unitCfg.PositionX},{unitCfg.PositionZ}");
                }
            }
            else
            {
                Debug.LogError($"[UnitSpawnerSystem] Prefab '{unitCfg.UnitPrefab}' not found in map!");
            }
        }

        prefabMap.Dispose();

        // 5. Запись в карту
        var mapEntity = SystemAPI.GetSingletonEntity<GridMapTag>();
        var mapBuffer = EntityManager.GetBuffer<GridCellElement>(mapEntity);

        foreach (var update in mapUpdates)
        {
            if (update.index < mapBuffer.Length)
            {
                var cell = mapBuffer[update.index];
                switch (update.layer)
                {
                    case UnitLayer.Ground:
                        cell.IsOccupiedGround = true;
                        cell.OccupantGround = update.unit;
                        break;
                    case UnitLayer.Sky:
                        cell.IsOccupiedSky = true;
                        cell.OccupantSky = update.unit;
                        break;
                    case UnitLayer.Underground:
                        cell.IsOccupiedUnderground = true;
                        cell.OccupantUnderground = update.unit;
                        break;
                }
                mapBuffer[update.index] = cell;
            }
        }

        _hasSpawned = true;
    }
}
