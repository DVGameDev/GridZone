using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// СИСТЕМА 2: Обновление активных VFX
/// Управляет lifetime, движением projectiles, follow логикой
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[BurstCompile]
public partial struct VFXUpdateSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<ActiveVFX>();
    }
    
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        // 1. Обновляем VFX Projectiles (летящие снаряды)
        UpdateProjectiles(ref state, deltaTime);
        
        // 2. Обновляем VFX Follow (следование за юнитом)
        UpdateFollowVFX(ref state);
        
        // 3. Обновляем VFX Beams (лучи)
        UpdateBeams(ref state);
        
        // 4. Обновляем Lifetime для всех ActiveVFX
        UpdateLifetime(ref state, deltaTime);
    }
    
    /// <summary>
    /// Обновляет позицию летящих снарядов
    /// </summary>
    [BurstCompile]
    private void UpdateProjectiles(ref SystemState state, float deltaTime)
    {
        var projectileJob = new UpdateProjectilesJob
        {
            DeltaTime = deltaTime
        };
        
        state.Dependency = projectileJob.ScheduleParallel(state.Dependency);
    }
    
    /// <summary>
    /// Обновляет VFX которые следуют за юнитами
    /// </summary>
    private void UpdateFollowVFX(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        
        var followJob = new UpdateFollowVFXJob
        {
            TransformLookup = transformLookup
        };
        
        state.Dependency = followJob.ScheduleParallel(state.Dependency);
    }
    
    /// <summary>
    /// Обновляет лучевые VFX
    /// </summary>
    private void UpdateBeams(ref SystemState state)
    {
        var transformLookup = SystemAPI.GetComponentLookup<LocalTransform>(true);
        
        foreach (var (beam, vfx, transform) 
            in SystemAPI.Query<RefRW<VFXBeam>, RefRO<ActiveVFX>, RefRW<LocalTransform>>())
        {
            // Если нужно обновлять конечную позицию (follow target)
            if (beam.ValueRO.UpdateEndPosition && vfx.ValueRO.TargetUnit != Entity.Null)
            {
                if (transformLookup.TryGetComponent(vfx.ValueRO.TargetUnit, out var targetTransform))
                {
                    beam.ValueRW.EndPosition = targetTransform.Position;
                }
            }
            
            // Обновляем позицию луча (центр между start и end)
            float3 midpoint = (beam.ValueRO.StartPosition + beam.ValueRO.EndPosition) * 0.5f;
            transform.ValueRW.Position = midpoint;
            
            // TODO: Обновить GameObject LineRenderer
        }
    }
    
    /// <summary>
    /// Обновляет время жизни всех VFX
    /// </summary>
    [BurstCompile]
    private void UpdateLifetime(ref SystemState state, float deltaTime)
    {
        var lifetimeJob = new UpdateLifetimeJob
        {
            DeltaTime = deltaTime
        };
        
        state.Dependency = lifetimeJob.ScheduleParallel(state.Dependency);
    }
}

/// <summary>
/// Job для обновления projectile VFX
/// </summary>
[BurstCompile]
partial struct UpdateProjectilesJob : IJobEntity
{
    public float DeltaTime;
    
    void Execute(
        ref LocalTransform transform,
        ref VFXProjectile projectile,
        ref ActiveVFX vfx)
    {
        // Если VFX ещё не стартовал (задержка), пропускаем
        if (vfx.TimeAlive < 0)
            return;
        
        // Если уже завершён, пропускаем
        if (vfx.IsComplete)
            return;
        
        // Вычисляем новую позицию
        float distanceThisFrame = projectile.Speed * DeltaTime;
        float3 movement = projectile.Direction * distanceThisFrame;
        
        transform.Position += movement;
        projectile.DistanceTraveled += distanceThisFrame;
        vfx.CurrentPosition = transform.Position;
        
        // Проверяем достижение цели
        if (projectile.DistanceTraveled >= projectile.MaxDistance)
        {
            // Достигли цели
            vfx.IsComplete = true;
            transform.Position = vfx.TargetPosition; // Финальная позиция
        }
        else
        {
            // Проверка близости к цели (для корректного попадания)
            float distanceToTarget = math.distance(transform.Position, vfx.TargetPosition);
            if (distanceToTarget < 0.1f)
            {
                vfx.IsComplete = true;
                transform.Position = vfx.TargetPosition;
            }
        }
    }
}

/// <summary>
/// Job для обновления VFX следующих за юнитами
/// </summary>
[BurstCompile]
partial struct UpdateFollowVFXJob : IJobEntity
{
    [ReadOnly] public ComponentLookup<LocalTransform> TransformLookup;
    
    void Execute(
        ref LocalTransform transform,
        ref ActiveVFX vfx,
        in VFXFollowUnit follow)
    {
        // Если VFX ещё не стартовал (задержка), пропускаем
        if (vfx.TimeAlive < 0)
            return;
        
        // Получаем позицию целевого юнита
        if (TransformLookup.TryGetComponent(follow.TargetUnit, out var targetTransform))
        {
            // Обновляем позицию VFX
            transform.Position = targetTransform.Position + follow.LocalOffset;
            vfx.CurrentPosition = transform.Position;
            
            // Если нужно, обновляем rotation
            if (follow.MatchRotation)
            {
                transform.Rotation = targetTransform.Rotation;
            }
        }
        else
        {
            // Целевой юнит не существует - завершаем VFX
            vfx.IsComplete = true;
        }
    }
}

/// <summary>
/// Job для обновления времени жизни VFX
/// </summary>
[BurstCompile]
partial struct UpdateLifetimeJob : IJobEntity
{
    public float DeltaTime;
    
    void Execute(ref ActiveVFX vfx)
    {
        // Обновляем время жизни
        vfx.TimeAlive += DeltaTime;
        
        // Проверяем, не истекла ли длительность
        // Duration = -1 означает infinite (аура)
        if (vfx.Duration > 0 && vfx.TimeAlive >= vfx.Duration)
        {
            vfx.IsComplete = true;
        }
        
        // VFX с отрицательным TimeAlive ещё не стартовали (задержка)
        // Они будут стартовать когда TimeAlive >= 0
    }
}
