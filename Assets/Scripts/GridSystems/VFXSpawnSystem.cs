using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// СИСТЕМА 1: Обработка запросов VFX
/// Создаёт GameObject с VFX prefab и Entity для управления
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class VFXSpawnSystem : SystemBase
{
    private EntityQuery _vfxRequestQuery;
    
    protected override void OnCreate()
    {
        _vfxRequestQuery = GetEntityQuery(
            ComponentType.ReadOnly<VFXSpawnRequest>()
        );
        
        RequireForUpdate<VFXLibrary>();
    }
    
    protected override void OnUpdate()
    {
        // Получаем библиотеку VFX префабов
        if (!SystemAPI.TryGetSingleton<VFXLibrary>(out var vfxLibrary))
        {
            Debug.LogWarning("[VFXSpawnSystem] VFXLibrary not found!");
            return;
        }
        
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        
        // Обрабатываем все VFX запросы
        foreach (var (request, affectedCells, entity) 
            in SystemAPI.Query<RefRW<VFXSpawnRequest>, DynamicBuffer<VFXAffectedCell>>()
                .WithEntityAccess())
        {
            if (request.ValueRO.IsProcessed)
                continue;
            
            // Выбираем подходящий префаб из библиотеки
            Entity vfxPrefabEntity = SelectVFXPrefab(request.ValueRO.Type, vfxLibrary);
            
            if (vfxPrefabEntity == Entity.Null)
            {
                Debug.LogWarning($"[VFXSpawnSystem] No prefab found for VFX type: {request.ValueRO.Type}");
                request.ValueRW.IsProcessed = true;
                continue;
            }
            
            // Создаём VFX в зависимости от количества affected cells
            if (affectedCells.Length == 0)
            {
                // Single-target VFX
                SpawnSingleVFX(
                    request.ValueRO,
                    vfxPrefabEntity,
                    ecb
                );
            }
            else
            {
                // Multi-target VFX (для каждой клетки)
                SpawnMultiVFX(
                    request.ValueRO,
                    affectedCells,
                    vfxPrefabEntity,
                    ecb
                );
            }
            
            // Помечаем запрос как обработанный
            request.ValueRW.IsProcessed = true;
            
            // Удаляем запрос после обработки
            ecb.DestroyEntity(entity);
        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
    
    /// <summary>
    /// Создаёт один VFX эффект
    /// </summary>
    private void SpawnSingleVFX(
        VFXSpawnRequest request,
        Entity vfxPrefab,
        EntityCommandBuffer ecb)
    {
        // 1. Определяем начальную позицию
        float3 startPosition = CalculateOriginPosition(
            request.Config.Origin,
            request.SourceUnit,
            request.SourcePosition,
            request.Config.Offset
        );
        
        // 2. Определяем целевую позицию
        float3 targetPosition = CalculateTargetPosition(
            request.Config.Target,
            request.TargetUnit,
            request.TargetPosition,
            request.TargetCell
        );
        
        // 3. Создаём VFX entity
        Entity vfxEntity = ecb.Instantiate(vfxPrefab);
        ecb.AddComponent(vfxEntity, new VFXTag());
        
        // 4. Устанавливаем позицию
        ecb.SetComponent(vfxEntity, LocalTransform.FromPosition(startPosition));
        
        // 5. Добавляем ActiveVFX component
        ecb.AddComponent(vfxEntity, new ActiveVFX
        {
            Type = request.Type,
            Timing = request.Config.Timing,
            TimeAlive = 0f,
            Duration = request.Config.Duration,
            SourceUnit = request.SourceUnit,
            TargetUnit = request.TargetUnit,
            CurrentPosition = startPosition,
            TargetPosition = targetPosition,
            IsComplete = false,
            DestroyOnComplete = !request.Config.PersistAfterHit
        });
        
        // 6. Добавляем специфичные компоненты в зависимости от типа
        AddVFXTypeSpecificComponents(
            vfxEntity,
            request.Config,
            startPosition,
            targetPosition,
            ecb
        );
        
        // 7. Создаём GameObject для визуализации
        CreateVFXGameObject(
            vfxEntity,
            request.Type,
            startPosition,
            targetPosition,
            request.Config
        );
    }
    
    /// <summary>
    /// Создаёт множественные VFX эффекты (для каждой клетки)
    /// </summary>
    private void SpawnMultiVFX(
        VFXSpawnRequest request,
        DynamicBuffer<VFXAffectedCell> affectedCells,
        Entity vfxPrefab,
        EntityCommandBuffer ecb)
    {
        float3 sourcePosition = CalculateOriginPosition(
            request.Config.Origin,
            request.SourceUnit,
            request.SourcePosition,
            request.Config.Offset
        );
        
        // Создаём VFX для каждой affected клетки
        for (int i = 0; i < affectedCells.Length; i++)
        {
            var cell = affectedCells[i];
            
            // Создаём VFX entity
            Entity vfxEntity = ecb.Instantiate(vfxPrefab);
            ecb.AddComponent(vfxEntity, new VFXTag());
            
            // Устанавливаем позицию (с небольшой задержкой для каждой клетки)
            float cellDelay = request.Config.Delay + (i * 0.05f); // 50ms между клетками
            
            ecb.SetComponent(vfxEntity, LocalTransform.FromPosition(cell.WorldPos));
            
            ecb.AddComponent(vfxEntity, new ActiveVFX
            {
                Type = request.Type,
                Timing = request.Config.Timing,
                TimeAlive = -cellDelay, // Отрицательное время = задержка
                Duration = request.Config.Duration,
                SourceUnit = request.SourceUnit,
                TargetUnit = Entity.Null,
                CurrentPosition = cell.WorldPos,
                TargetPosition = cell.WorldPos,
                IsComplete = false,
                DestroyOnComplete = true
            });
            
            // Добавляем компонент области
            ecb.AddComponent(vfxEntity, new VFXAreaEffect
            {
                CenterCell = cell.GridPos,
                Radius = request.Config.Scale,
                PulseEffect = false,
                PulseSpeed = 1f
            });
            
            // Создаём GameObject
            CreateVFXGameObject(
                vfxEntity,
                request.Type,
                cell.WorldPos,
                cell.WorldPos,
                request.Config
            );
        }
    }
    
    /// <summary>
    /// Добавляет компоненты специфичные для типа VFX
    /// </summary>
    private void AddVFXTypeSpecificComponents(
        Entity vfxEntity,
        VFXConfig config,
        float3 startPos,
        float3 targetPos,
        EntityCommandBuffer ecb)
    {
        // Projectile VFX
        if (IsProjectileVFX(config.Type))
        {
            float3 direction = math.normalize(targetPos - startPos);
            float distance = math.distance(startPos, targetPos);
            
            ecb.AddComponent(vfxEntity, new VFXProjectile
            {
                Speed = config.Speed > 0 ? config.Speed : 10f,
                Direction = direction,
                DistanceTraveled = 0f,
                MaxDistance = distance,
                HomingTarget = false,
                TargetEntity = Entity.Null
            });
        }
        
        // Beam VFX
        else if (IsBeamVFX(config.Type))
        {
            ecb.AddComponent(vfxEntity, new VFXBeam
            {
                StartPosition = startPos,
                EndPosition = targetPos,
                Width = config.Scale > 0 ? config.Scale : 0.2f,
                UpdateEndPosition = config.FollowTarget
            });
        }
        
        // Aura / Follow VFX
        else if (config.AttachToUnit || config.FollowTarget)
        {
            ecb.AddComponent(vfxEntity, new VFXFollowUnit
            {
                TargetUnit = config.Target == VFXTarget.ToSelf 
                    ? vfxEntity  // TODO: получить source unit
                    : Entity.Null,
                LocalOffset = config.Offset,
                MatchRotation = true
            });
        }
    }
    
    /// <summary>
    /// Создаёт GameObject с VFX визуализацией
    /// NOTE: В реальной реализации здесь должен быть Instantiate Unity Prefab
    /// </summary>
    private void CreateVFXGameObject(
        Entity vfxEntity,
        VFXType vfxType,
        float3 position,
        float3 targetPosition,
        VFXConfig config)
    {
        // TODO: Реализовать создание GameObject из префаба
        // GameObject vfxGO = Object.Instantiate(vfxPrefab, position, rotation);
        // ParticleSystem ps = vfxGO.GetComponent<ParticleSystem>();
        
        // Пока что это placeholder
        // В реальной реализации:
        // 1. Получить префаб из Resources или VFXLibrary
        // 2. Instantiate GameObject
        // 3. Настроить ParticleSystem / VFX Graph
        // 4. Применить ColorTint, Scale
        // 5. Добавить VFXGameObjectReference к entity
        
        Debug.Log($"[VFXSpawnSystem] Created VFX: {vfxType} at {position}");
    }
    
    // === HELPER METHODS ===
    
    private float3 CalculateOriginPosition(
        VFXOrigin origin,
        Entity sourceUnit,
        float3 defaultPosition,
        float3 offset)
    {
        // TODO: В реальной реализации получать позицию из Transform компонента
        // sourceUnit и добавлять offset в зависимости от origin типа
        
        float3 position = defaultPosition;
        
        switch (origin)
        {
            case VFXOrigin.AtCasterHand:
                position += new float3(0, 1f, 0); // Offset вверх для руки
                break;
            case VFXOrigin.AtCasterWeapon:
                position += new float3(0, 1.5f, 0); // Offset для оружия
                break;
            case VFXOrigin.AtCasterHead:
                position += new float3(0, 2f, 0);
                break;
            case VFXOrigin.AtCasterFeet:
                position += new float3(0, 0.1f, 0);
                break;
        }
        
        return position + offset;
    }
    
    private float3 CalculateTargetPosition(
        VFXTarget target,
        Entity targetUnit,
        float3 defaultPosition,
        int2 targetCell)
    {
        // TODO: В реальной реализации получать позицию из Transform или Grid
        return defaultPosition;
    }
    
    private Entity SelectVFXPrefab(VFXType type, VFXLibrary library)
    {
        // Маппинг VFXType -> Entity prefab
        return type switch
        {
            VFXType.Fireball => library.FireballPrefab,
            VFXType.Arrow => library.ArrowPrefab,
            VFXType.LightningBolt => library.LightningBoltPrefab,
            VFXType.IceShard => library.IceShardPrefab,
            VFXType.MagicMissile => library.MagicMissilePrefab,
            
            VFXType.Explosion => library.ExplosionPrefab,
            VFXType.SlashEffect => library.SlashEffectPrefab,
            VFXType.ImpactSparks => library.ImpactSparksPrefab,
            VFXType.HealGlow => library.HealGlowPrefab,
            VFXType.BuffGlow => library.BuffGlowPrefab,
            
            VFXType.FireCone => library.FireConePrefab,
            VFXType.PoisonCloud => library.PoisonCloudPrefab,
            VFXType.IceRing => library.IceRingPrefab,
            VFXType.HolyLightCross => library.HolyLightCrossPrefab,
            VFXType.Shockwave => library.ShockwavePrefab,
            
            VFXType.HealBeam => library.HealBeamPrefab,
            VFXType.DamageBeam => library.DamageBeamPrefab,
            VFXType.LightningBeam => library.LightningBeamPrefab,
            
            VFXType.ShieldAura => library.ShieldAuraPrefab,
            VFXType.PoisonAura => library.PoisonAuraPrefab,
            VFXType.BuffAura => library.BuffAuraPrefab,
            VFXType.RegenAura => library.RegenAuraPrefab,
            
            VFXType.Teleport => library.TeleportPrefab,
            VFXType.Summon => library.SummonPrefab,
            VFXType.Resurrection => library.ResurrectionPrefab,
            VFXType.Shapeshift => library.ShapeshiftPrefab,
            
            _ => Entity.Null
        };
    }
    
    private bool IsProjectileVFX(VFXType type)
    {
        return type switch
        {
            VFXType.Fireball => true,
            VFXType.Arrow => true,
            VFXType.LightningBolt => true,
            VFXType.IceShard => true,
            VFXType.MagicMissile => true,
            VFXType.Spear => true,
            VFXType.Axe => true,
            VFXType.Dagger => true,
            VFXType.Boulder => true,
            VFXType.AcidBlob => true,
            _ => false
        };
    }
    
    private bool IsBeamVFX(VFXType type)
    {
        return type switch
        {
            VFXType.HealBeam => true,
            VFXType.DamageBeam => true,
            VFXType.LightningBeam => true,
            VFXType.LaserBeam => true,
            VFXType.DrainBeam => true,
            VFXType.ChainLightning => true,
            _ => false
        };
    }
}
