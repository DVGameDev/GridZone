using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Библиотека VFX префабов для разных типов эффектов
/// Singleton на Bootstrap сущности
/// </summary>
public struct VFXLibrary : IComponentData
{
    // === PROJECTILES ===
    public Entity FireballPrefab;
    public Entity ArrowPrefab;
    public Entity LightningBoltPrefab;
    public Entity IceShardPrefab;
    public Entity MagicMissilePrefab;
    
    // === IMPACTS ===
    public Entity ExplosionPrefab;
    public Entity ImpactSparksPrefab;
    public Entity SlashEffectPrefab;
    public Entity HealGlowPrefab;
    public Entity BuffGlowPrefab;
    
    // === AREA EFFECTS (Persistent) ===
    public Entity FireConePrefab;
    public Entity PoisonCloudPrefab;
    public Entity IceRingPrefab;
    public Entity HolyLightCrossPrefab;
    public Entity ShockwavePrefab;
    
    // === BEAMS ===
    public Entity HealBeamPrefab;
    public Entity DamageBeamPrefab;
    public Entity LightningBeamPrefab;
    
    // === AURAS (Around Unit) ===
    public Entity ShieldAuraPrefab;
    public Entity PoisonAuraPrefab;
    public Entity BuffAuraPrefab;
    public Entity RegenAuraPrefab;
    
    // === SPECIAL ===
    public Entity TeleportPrefab;
    public Entity SummonPrefab;
    public Entity ResurrectionPrefab;
    public Entity ShapeshiftPrefab;
}

/// <summary>
/// Конфигурация конкретного VFX эффекта
/// Хранится на картах/способностях
/// </summary>
[Serializable]
public struct VFXConfig
{
    public VFXType Type;           // Тип визуального эффекта
    public VFXTiming Timing;       // Когда проигрывать
    public VFXOrigin Origin;       // Откуда начинается
    public VFXTarget Target;       // Куда направлен
    
    public float Duration;         // Длительность эффекта (сек), -1 = infinite
    public float Delay;            // Задержка перед стартом (сек)
    public float Speed;            // Скорость для projectile/beam
    
    public float3 Offset;          // Смещение от origin точки
    public float Scale;            // Масштаб эффекта
    public float4 ColorTint;       // Окраска эффекта
    
    public bool FollowTarget;      // Следовать за целью?
    public bool AttachToUnit;      // Привязать к юниту?
    public bool PersistAfterHit;   // Оставить после попадания?
}

/// <summary>
/// Запрос на создание VFX эффекта
/// Создаётся системой эффектов, обрабатывается VFX системой
/// </summary>
public struct VFXSpawnRequest : IComponentData
{
    public VFXType Type;
    public VFXConfig Config;
    
    public Entity SourceUnit;      // Кто кастует
    public Entity TargetUnit;      // На кого (может быть Null)
    
    public float3 SourcePosition;  // Откуда
    public float3 TargetPosition;  // Куда
    
    public int2 TargetCell;        // Целевая клетка (для single-target)
    
    public bool IsProcessed;       // Обработан ли запрос
}

/// <summary>
/// Буфер affected cells для multi-target VFX
/// Добавляется к VFXSpawnRequest entity
/// </summary>
public struct VFXAffectedCell : IBufferElementData
{
    public int2 GridPos;           // Координаты клетки
    public float3 WorldPos;        // Мировые координаты
}

/// <summary>
/// Активный VFX инстанс в мире
/// </summary>
public struct ActiveVFX : IComponentData
{
    public VFXType Type;
    public VFXTiming Timing;
    
    public float TimeAlive;        // Сколько уже живёт (сек)
    public float Duration;         // Общая длительность (сек), -1 = infinite
    
    public Entity SourceUnit;      // Кто создал
    public Entity TargetUnit;      // На кого направлен
    
    public float3 CurrentPosition; // Текущая позиция
    public float3 TargetPosition;  // Целевая позиция
    
    public bool IsComplete;        // Завершён ли эффект
    public bool DestroyOnComplete; // Удалить после завершения?
}

/// <summary>
/// VFX который следует за юнитом (аура, бафф, дебафф)
/// </summary>
public struct VFXFollowUnit : IComponentData
{
    public Entity TargetUnit;      // За кем следовать
    public float3 LocalOffset;     // Локальное смещение
    public bool MatchRotation;     // Вращаться вместе с юнитом?
}

/// <summary>
/// VFX projectile (летящий снаряд)
/// </summary>
public struct VFXProjectile : IComponentData
{
    public float Speed;            // Скорость полёта
    public float3 Direction;       // Направление (normalized)
    public float DistanceTraveled; // Пройденное расстояние
    public float MaxDistance;      // Максимальная дистанция
    
    public bool HomingTarget;      // Самонаводящийся?
    public Entity TargetEntity;    // Цель для homing
}

/// <summary>
/// VFX Beam (луч между двумя точками)
/// </summary>
public struct VFXBeam : IComponentData
{
    public float3 StartPosition;   // Начальная точка
    public float3 EndPosition;     // Конечная точка
    public float Width;            // Толщина луча
    public bool UpdateEndPosition; // Обновлять конечную точку?
}

/// <summary>
/// VFX Area Effect (область на земле)
/// </summary>
public struct VFXAreaEffect : IComponentData
{
    public int2 CenterCell;        // Центральная клетка
    public float Radius;           // Радиус области
    public bool PulseEffect;       // Пульсирующий эффект?
    public float PulseSpeed;       // Скорость пульсации
}

/// <summary>
/// Буфер эффектов для мультикастовых способностей / комбо
/// Добавляется на Unit Entity
/// </summary>
public struct VFXQueueElement : IBufferElementData
{
    public VFXConfig Config;
    public float TriggerTime;      // Когда проигрывать (game time)
    public int2 TargetCell;        // Куда направлен
    public Entity TargetUnit;      // Целевой юнит (если есть)
}

/// <summary>
/// Ссылка на GameObject с VFX
/// Managed component для хранения GameObject reference
/// </summary>
public class VFXGameObjectReference : IComponentData
{
    public UnityEngine.GameObject GameObject;
    public UnityEngine.ParticleSystem ParticleSystem;
    public UnityEngine.LineRenderer LineRenderer;
}

/// <summary>
/// Тег для VFX entities (для удобства фильтрации)
/// </summary>
public struct VFXTag : IComponentData { }

/// <summary>
/// Типы визуальных эффектов
/// </summary>
public enum VFXType : byte
{
    None,
    
    // === PROJECTILES ===
    Fireball,
    Arrow,
    IceShard,
    LightningBolt,
    MagicMissile,
    Spear,
    Axe,
    Dagger,
    Boulder,
    AcidBlob,
    
    // === IMPACTS ===
    Explosion,
    SlashEffect,
    ImpactSparks,
    HealGlow,
    BuffGlow,
    DebuffGlow,
    SmokePuff,
    BloodSplatter,
    IceShatter,
    LightningStrike,
    
    // === AREA EFFECTS (Persistent) ===
    FireCone,
    PoisonCloud,
    IceRing,
    HolyLightCross,
    Shockwave,
    Earthquake,
    Tornado,
    Blizzard,
    AcidPool,
    LavaGround,
    
    // === BEAMS ===
    HealBeam,
    DamageBeam,
    LightningBeam,
    LaserBeam,
    DrainBeam,
    ChainLightning,
    
    // === AURAS (Around Unit) ===
    ShieldAura,
    PoisonAura,
    BuffAura,
    RegenAura,
    FireAura,
    IceAura,
    HolyAura,
    DarkAura,
    
    // === SPECIAL ===
    Teleport,
    Summon,
    Resurrection,
    Shapeshift,
    Vanish,
    Appear,
    LevelUp,
    Death
}

/// <summary>
/// Когда проигрывать эффект
/// </summary>
public enum VFXTiming : byte
{
    OnCast,           // При касте (начало анимации)
    OnHit,            // При попадании (конец projectile)
    OnDamage,         // Когда урон нанесён
    OnHeal,           // Когда хил применён
    OnBuff,           // Когда бафф наложен
    OnDebuff,         // Когда дебафф наложен
    WhileChanneling,  // Во время чтения заклинания
    OnComplete,       // Когда эффект завершён
    Persistent,       // Постоянный (аура)
    OnDeath,          // При смерти
    OnSummon,         // При призыве
    OnTeleport        // При телепортации
}

/// <summary>
/// Откуда начинается эффект
/// </summary>
public enum VFXOrigin : byte
{
    AtCaster,         // От центра кастера
    AtCasterHand,     // От руки кастера
    AtCasterWeapon,   // От оружия
    AtCasterHead,     // От головы
    AtCasterFeet,     // От ног
    AtTarget,         // На цели
    AtCursor,         // На курсоре
    AtCell,           // На клетке
    AtMultipleCells,  // На нескольких клетках
    AtWorld           // В конкретной мировой точке
}

/// <summary>
/// Куда направлен эффект
/// </summary>
public enum VFXTarget : byte
{
    ToSelf,           // На себя
    ToTarget,         // На цель
    ToCursor,         // К курсору
    ToCell,           // К клетке
    ToMultipleCells,  // К нескольким клеткам
    InDirection,      // В направлении (для конусов)
    ToGround,         // На землю
    ToSky             // В небо
}
