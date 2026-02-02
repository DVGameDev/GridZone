using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Анимирует пульсацию overlay (изменение альфы)
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(AreaOverlaySystem))]
public partial class AreaOverlayAnimationSystem : SystemBase
{
    protected override void OnUpdate()
    {
        float time = (float)SystemAPI.Time.ElapsedTime;

        foreach (var (animData, overlayData)
            in SystemAPI.Query<RefRW<OverlayAnimationData>, RefRO<ActiveOverlayData>>())
        {
            // Обновляем фазу пульсации
            animData.ValueRW.PulsePhase = (time * animData.ValueRO.PulseSpeed) % 1.0f;

            // Применяем к материалу
            if (overlayData.ValueRO.MeshEntity != Entity.Null &&
                EntityManager.Exists(overlayData.ValueRO.MeshEntity) &&
                EntityManager.HasComponent<MeshRendererReference>(overlayData.ValueRO.MeshEntity))
            {
                var meshRef = EntityManager.GetComponentObject<MeshRendererReference>(overlayData.ValueRO.MeshEntity);

                if (meshRef != null && meshRef.Material != null)
                {
                    // Пульсация альфы через синусоиду
                    float baseAlpha = 0.15f;
                    float alphaDelta = math.sin(animData.ValueRO.PulsePhase * math.PI * 2f) * animData.ValueRO.PulseIntensity;
                    float alpha = baseAlpha + alphaDelta;

                    Color currentColor = meshRef.Material.GetColor("_BaseColor");
                    currentColor.a = alpha;
                    meshRef.Material.SetColor("_BaseColor", currentColor);
                }
            }
        }
    }
}
