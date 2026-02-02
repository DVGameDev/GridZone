using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Система создания визуальных GameObject для Entity
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
public partial class VisualInstantiationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // Создаем визуал для новых Entity
        foreach (var (prefab, transform, entity)
            in SystemAPI.Query<VisualPrefab, RefRO<LocalTransform>>()
                .WithAll<NeedsVisualInstantiation>()
                .WithEntityAccess())
        {
            if (prefab.Value == null)
            {
                Debug.LogError("[VisualInstantiation] Prefab is null!");
                ecb.RemoveComponent<NeedsVisualInstantiation>(entity);
                continue;
            }

            // 🔥 СОЗДАЕМ GameObject из prefab
            var visualGO = Object.Instantiate(
                prefab.Value,
                transform.ValueRO.Position,
                transform.ValueRO.Rotation
            );

            visualGO.name = $"Visual_{entity.Index}";

            // Получаем Animator
            var animator = visualGO.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                Debug.LogError("[VisualInstantiation] No Animator found!");
            }

            // Добавляем ссылку на GameObject
            ecb.AddComponent(entity, new VisualGameObject
            {
                Value = visualGO,
                Animator = animator
            });

            // Добавляем состояние анимации
            ecb.AddComponent(entity, new AnimationState { IsWalking = false });

            // Удаляем тег
            ecb.RemoveComponent<NeedsVisualInstantiation>(entity);

            Debug.Log($"✅ [VisualInstantiation] Created visual for Entity {entity.Index}");
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
