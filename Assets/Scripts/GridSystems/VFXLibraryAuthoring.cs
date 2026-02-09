using Unity.Entities;
using UnityEngine;

/// <summary>
/// Authoring компонент для VFX Library
/// Добавляется на Bootstrap GameObject
/// </summary>
public class VFXLibraryAuthoring : MonoBehaviour
{
    [Header("Projectile VFX")]
    public GameObject FireballPrefab;
    public GameObject ArrowPrefab;
    public GameObject LightningBoltPrefab;
    public GameObject IceShardPrefab;
    public GameObject MagicMissilePrefab;
    
    [Header("Impact VFX")]
    public GameObject ExplosionPrefab;
    public GameObject ImpactSparksPrefab;
    public GameObject SlashEffectPrefab;
    public GameObject HealGlowPrefab;
    public GameObject BuffGlowPrefab;
    
    [Header("Area Effects")]
    public GameObject FireConePrefab;
    public GameObject PoisonCloudPrefab;
    public GameObject IceRingPrefab;
    public GameObject HolyLightCrossPrefab;
    public GameObject ShockwavePrefab;
    
    [Header("Beams")]
    public GameObject HealBeamPrefab;
    public GameObject DamageBeamPrefab;
    public GameObject LightningBeamPrefab;
    
    [Header("Auras")]
    public GameObject ShieldAuraPrefab;
    public GameObject PoisonAuraPrefab;
    public GameObject BuffAuraPrefab;
    public GameObject RegenAuraPrefab;
    
    [Header("Special")]
    public GameObject TeleportPrefab;
    public GameObject SummonPrefab;
    public GameObject ResurrectionPrefab;
    public GameObject ShapeshiftPrefab;
    
    class Baker : Baker<VFXLibraryAuthoring>
    {
        public override void Bake(VFXLibraryAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new VFXLibrary
            {
                // Projectiles
                FireballPrefab = GetEntity(authoring.FireballPrefab, TransformUsageFlags.Dynamic),
                ArrowPrefab = GetEntity(authoring.ArrowPrefab, TransformUsageFlags.Dynamic),
                LightningBoltPrefab = GetEntity(authoring.LightningBoltPrefab, TransformUsageFlags.Dynamic),
                IceShardPrefab = GetEntity(authoring.IceShardPrefab, TransformUsageFlags.Dynamic),
                MagicMissilePrefab = GetEntity(authoring.MagicMissilePrefab, TransformUsageFlags.Dynamic),
                
                // Impacts
                ExplosionPrefab = GetEntity(authoring.ExplosionPrefab, TransformUsageFlags.Dynamic),
                ImpactSparksPrefab = GetEntity(authoring.ImpactSparksPrefab, TransformUsageFlags.Dynamic),
                SlashEffectPrefab = GetEntity(authoring.SlashEffectPrefab, TransformUsageFlags.Dynamic),
                HealGlowPrefab = GetEntity(authoring.HealGlowPrefab, TransformUsageFlags.Dynamic),
                BuffGlowPrefab = GetEntity(authoring.BuffGlowPrefab, TransformUsageFlags.Dynamic),
                
                // Area Effects
                FireConePrefab = GetEntity(authoring.FireConePrefab, TransformUsageFlags.Dynamic),
                PoisonCloudPrefab = GetEntity(authoring.PoisonCloudPrefab, TransformUsageFlags.Dynamic),
                IceRingPrefab = GetEntity(authoring.IceRingPrefab, TransformUsageFlags.Dynamic),
                HolyLightCrossPrefab = GetEntity(authoring.HolyLightCrossPrefab, TransformUsageFlags.Dynamic),
                ShockwavePrefab = GetEntity(authoring.ShockwavePrefab, TransformUsageFlags.Dynamic),
                
                // Beams
                HealBeamPrefab = GetEntity(authoring.HealBeamPrefab, TransformUsageFlags.Dynamic),
                DamageBeamPrefab = GetEntity(authoring.DamageBeamPrefab, TransformUsageFlags.Dynamic),
                LightningBeamPrefab = GetEntity(authoring.LightningBeamPrefab, TransformUsageFlags.Dynamic),
                
                // Auras
                ShieldAuraPrefab = GetEntity(authoring.ShieldAuraPrefab, TransformUsageFlags.Dynamic),
                PoisonAuraPrefab = GetEntity(authoring.PoisonAuraPrefab, TransformUsageFlags.Dynamic),
                BuffAuraPrefab = GetEntity(authoring.BuffAuraPrefab, TransformUsageFlags.Dynamic),
                RegenAuraPrefab = GetEntity(authoring.RegenAuraPrefab, TransformUsageFlags.Dynamic),
                
                // Special
                TeleportPrefab = GetEntity(authoring.TeleportPrefab, TransformUsageFlags.Dynamic),
                SummonPrefab = GetEntity(authoring.SummonPrefab, TransformUsageFlags.Dynamic),
                ResurrectionPrefab = GetEntity(authoring.ResurrectionPrefab, TransformUsageFlags.Dynamic),
                ShapeshiftPrefab = GetEntity(authoring.ShapeshiftPrefab, TransformUsageFlags.Dynamic),
            });
        }
    }
}

/// <summary>
/// Authoring компонент для настройки VFX Config на способностях/картах
/// Добавляется на Prefab способности
/// </summary>
[System.Serializable]
public class VFXConfigAuthoring
{
    [Header("VFX Type")]
    public VFXType Type = VFXType.None;
    
    [Header("Timing")]
    public VFXTiming Timing = VFXTiming.OnCast;
    
    [Header("Origin & Target")]
    public VFXOrigin Origin = VFXOrigin.AtCaster;
    public VFXTarget Target = VFXTarget.ToTarget;
    
    [Header("Parameters")]
    public float Duration = 1.0f;
    public float Delay = 0f;
    public float Speed = 10f;
    
    [Header("Visual")]
    public Vector3 Offset = Vector3.zero;
    public float Scale = 1.0f;
    public Color ColorTint = Color.white;
    
    [Header("Behavior")]
    public bool FollowTarget = false;
    public bool AttachToUnit = false;
    public bool PersistAfterHit = false;
    
    /// <summary>
    /// Конвертирует Authoring в ECS компонент
    /// </summary>
    public VFXConfig ToVFXConfig()
    {
        return new VFXConfig
        {
            Type = Type,
            Timing = Timing,
            Origin = Origin,
            Target = Target,
            Duration = Duration,
            Delay = Delay,
            Speed = Speed,
            Offset = Offset,
            Scale = Scale,
            ColorTint = new Unity.Mathematics.float4(
                ColorTint.r, ColorTint.g, ColorTint.b, ColorTint.a
            ),
            FollowTarget = FollowTarget,
            AttachToUnit = AttachToUnit,
            PersistAfterHit = PersistAfterHit
        };
    }
}

/// <summary>
/// Пример Authoring компонента для способности с VFX
/// </summary>
public class AbilityVFXAuthoring : MonoBehaviour
{
    [Header("VFX Configuration")]
    public VFXConfigAuthoring OnCastVFX;
    public VFXConfigAuthoring OnHitVFX;
    public VFXConfigAuthoring PersistentVFX;
    
    class Baker : Baker<AbilityVFXAuthoring>
    {
        public override void Bake(AbilityVFXAuthoring authoring)
        {
            // Пример использования - добавляем VFX конфигурацию к ability entity
            // В реальной реализации это будет интегрировано в EffectCfg
            
            // var entity = GetEntity(TransformUsageFlags.None);
            // AddComponent(entity, authoring.OnCastVFX.ToVFXConfig());
        }
    }
}
