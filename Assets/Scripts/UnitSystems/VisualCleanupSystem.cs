using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Удаляем GameObject когда Entity уничтожается
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
public partial class VisualCleanupSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // Cleanup для удаленных Entity
        foreach (var (visualGO, entity)
            in SystemAPI.Query<VisualGameObject>()
                .WithNone<LocalTransform>() // Entity уничтожен
                .WithEntityAccess())
        {
            if (visualGO.Value != null)
            {
                Object.Destroy(visualGO.Value);
                Debug.Log($"🗑️ Destroyed visual for Entity {entity.Index}");
            }

            ecb.RemoveComponent<VisualGameObject>(entity);
        }

        ecb.Playback(EntityManager);
        ecb.Dispose();
    }
}
