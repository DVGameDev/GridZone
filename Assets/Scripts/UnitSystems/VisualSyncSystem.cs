using Unity.Entities;
using Unity.Transforms;

/// <summary>
/// Синхронизация Transform: Entity → GameObject
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitMoveSystem))]
public partial class VisualSyncSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Синхронизируем позиции
        foreach (var (visualGO, transform)
            in SystemAPI.Query<VisualGameObject, RefRO<LocalTransform>>())
        {
            if (visualGO.Value == null) continue;

            visualGO.Value.transform.position = transform.ValueRO.Position;
            visualGO.Value.transform.rotation = transform.ValueRO.Rotation;
        }
    }
}
