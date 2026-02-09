using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// СИСТЕМА 3: VFX Cleanup
/// Удаляет завершённые эффекты и их GameObjects
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateAfter(typeof(VFXUpdateSystem))]
public partial class VFXCleanupSystem : SystemBase
{
    private EntityQuery _completedVFXQuery;
    
    protected override void OnCreate()
    {
        // Query для завершённых VFX
        _completedVFXQuery = GetEntityQuery(
            ComponentType.ReadOnly<ActiveVFX>(),
            ComponentType.ReadOnly<VFXTag>()
        );
    }
    
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        
        // Находим все завершённые VFX
        foreach (var (vfx, vfxRef, entity) 
            in SystemAPI.Query<RefRO<ActiveVFX>, VFXGameObjectReference>()
                .WithAll<VFXTag>()
                .WithEntityAccess())
        {
            // Проверяем, завершён ли VFX
            if (!vfx.ValueRO.IsComplete)
                continue;
            
            // Проверяем, нужно ли удалять после завершения
            if (!vfx.ValueRO.DestroyOnComplete)
                continue;
            
            // Удаляем GameObject
            if (vfxRef != null && vfxRef.GameObject != null)
            {
                Object.Destroy(vfxRef.GameObject);
            }
            
            // Удаляем Entity
            ecb.DestroyEntity(entity);
        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}

/// <summary>
/// СИСТЕМА 4: VFX Queue Manager
/// Обрабатывает очереди VFX для комбо/цепных способностей
/// </summary>
[UpdateInGroup(typeof(PresentationSystemGroup))]
[UpdateBefore(typeof(VFXSpawnSystem))]
public partial class VFXQueueSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float currentTime = (float)SystemAPI.Time.ElapsedTime;
        var ecb = new EntityCommandBuffer(Allocator.TempJob);
        
        // Проходим по всем сущностям с VFX Queue
        foreach (var (queueBuffer, entity) 
            in SystemAPI.Query<DynamicBuffer<VFXQueueElement>>()
                .WithEntityAccess())
        {
            // Проверяем каждый элемент очереди
            for (int i = queueBuffer.Length - 1; i >= 0; i--)
            {
                var queueElement = queueBuffer[i];
                
                // Проверяем, пришло ли время триггерить VFX
                if (currentTime >= queueElement.TriggerTime)
                {
                    // Создаём VFX запрос
                    Entity vfxRequestEntity = ecb.CreateEntity();
                    
                    ecb.AddComponent(vfxRequestEntity, new VFXSpawnRequest
                    {
                        Type = queueElement.Config.Type,
                        Config = queueElement.Config,
                        SourceUnit = entity, // Юнит с очередью
                        TargetUnit = queueElement.TargetUnit,
                        SourcePosition = float3.zero, // TODO: получить из Transform
                        TargetPosition = float3.zero, // TODO: получить из cell
                        TargetCell = queueElement.TargetCell,
                        IsProcessed = false
                    });
                    
                    ecb.AddBuffer<VFXAffectedCell>(vfxRequestEntity);
                    
                    // Удаляем элемент из очереди
                    queueBuffer.RemoveAt(i);
                }
            }
            
            // Если очередь пуста, можно удалить буфер (опционально)
            // if (queueBuffer.Length == 0)
            // {
            //     ecb.RemoveComponent<VFXQueueElement>(entity);
            // }
        }
        
        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
