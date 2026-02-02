using Unity.Entities;
using Unity.Burst;

/// <summary>
/// Управление анимациями
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(UnitMoveSystem))]
public partial class AnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // 🔥 ОПТИМИЗАЦИЯ: Обрабатываем только изменившиеся состояния
        foreach (var (visualGO, animState, moveCmd)
            in SystemAPI.Query<VisualGameObject, RefRW<AnimationState>, RefRO<MoveCommand>>())
        {
            if (visualGO.Animator == null) continue;

            bool shouldWalk = moveCmd.ValueRO.IsMoving;

            // Обновляем только если изменилось
            if (animState.ValueRO.IsWalking != shouldWalk)
            {
                animState.ValueRW.IsWalking = shouldWalk;
                visualGO.Animator.SetBool("IsWalking", shouldWalk);
                // Убрал Debug.Log для производительности (вызывается каждый кадр)
            }
        }
    }
}
