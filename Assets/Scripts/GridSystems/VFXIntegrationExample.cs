using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// ПРИМЕР: Расширение EffectCfg для поддержки VFX
/// Добавьте эти поля в существующий EffectCfg в EffectComponents.cs
/// </summary>
public static class EffectCfgVFXExtension
{
    // В EffectCfg добавить:
    // public VFXConfig OnCastVFX;     // VFX при касте
    // public VFXConfig OnHitVFX;      // VFX при попадании
    // public VFXConfig PersistentVFX; // VFX который висит на юните
}

/// <summary>
/// ПРИМЕР: Helper для создания VFX запросов из эффектов
/// Используется в системе обработки эффектов
/// </summary>
public static class VFXRequestHelper
{
    /// <summary>
    /// Создаёт VFX запрос для single-target эффекта
    /// </summary>
    public static Entity CreateSingleTargetVFXRequest(
        EntityCommandBuffer ecb,
        VFXConfig config,
        Entity sourceUnit,
        Entity targetUnit,
        float3 sourcePosition,
        float3 targetPosition,
        int2 targetCell)
    {
        Entity requestEntity = ecb.CreateEntity();
        
        ecb.AddComponent(requestEntity, new VFXSpawnRequest
        {
            Type = config.Type,
            Config = config,
            SourceUnit = sourceUnit,
            TargetUnit = targetUnit,
            SourcePosition = sourcePosition,
            TargetPosition = targetPosition,
            TargetCell = targetCell,
            IsProcessed = false
        });
        
        // Пустой буфер для single-target
        ecb.AddBuffer<VFXAffectedCell>(requestEntity);
        
        return requestEntity;
    }
    
    /// <summary>
    /// Создаёт VFX запрос для multi-target эффекта (область)
    /// </summary>
    public static Entity CreateMultiTargetVFXRequest(
        EntityCommandBuffer ecb,
        VFXConfig config,
        Entity sourceUnit,
        float3 sourcePosition,
        NativeList<int2> affectedCells,
        GridConfig gridConfig)
    {
        Entity requestEntity = ecb.CreateEntity();
        
        ecb.AddComponent(requestEntity, new VFXSpawnRequest
        {
            Type = config.Type,
            Config = config,
            SourceUnit = sourceUnit,
            TargetUnit = Entity.Null,
            SourcePosition = sourcePosition,
            TargetPosition = sourcePosition, // Для area effects цель = источник
            TargetCell = affectedCells.Length > 0 ? affectedCells[0] : int2.zero,
            IsProcessed = false
        });
        
        // Заполняем буфер affected cells
        var cellBuffer = ecb.AddBuffer<VFXAffectedCell>(requestEntity);
        
        foreach (var cellPos in affectedCells)
        {
            float3 worldPos = GridToWorldPosition(cellPos, gridConfig);
            cellBuffer.Add(new VFXAffectedCell
            {
                GridPos = cellPos,
                WorldPos = worldPos
            });
        }
        
        return requestEntity;
    }
    
    /// <summary>
    /// Добавляет VFX в очередь юнита для отложенного воспроизведения
    /// </summary>
    public static void QueueVFX(
        EntityCommandBuffer ecb,
        Entity unitEntity,
        VFXConfig config,
        float triggerTime,
        int2 targetCell,
        Entity targetUnit = default)
    {
        // Получаем или создаём буфер очереди
        DynamicBuffer<VFXQueueElement> queueBuffer;
        
        // NOTE: В реальной реализации нужно проверить, есть ли уже буфер
        // Для примера просто добавляем элемент
        
        ecb.AppendToBuffer(unitEntity, new VFXQueueElement
        {
            Config = config,
            TriggerTime = triggerTime,
            TargetCell = targetCell,
            TargetUnit = targetUnit
        });
    }
    
    /// <summary>
    /// Конвертирует grid координаты в world позицию
    /// </summary>
    private static float3 GridToWorldPosition(int2 gridPos, GridConfig config)
    {
        if (config.Layout == GridLayoutType.Quad)
        {
            return new float3(
                gridPos.x * config.Spacing,
                config.HeightGround,
                gridPos.y * config.Spacing
            );
        }
        else // Hex
        {
            return HexGridUtils.HexAxialToWorld(gridPos, config.Spacing);
        }
    }
}

/// <summary>
/// ПРИМЕР: Интеграция в систему обработки эффектов
/// Добавьте этот код в вашу существующую систему эффектов
/// </summary>
public static class EffectSystemVFXIntegration
{
    /// <summary>
    /// Пример вызова VFX при применении эффекта
    /// </summary>
    public static void ApplyEffectWithVFX(
        EntityCommandBuffer ecb,
        EffectCfg effectCfg,
        Entity sourceUnit,
        Entity targetUnit,
        float3 sourcePosition,
        float3 targetPosition,
        int2 targetCell,
        GridConfig gridConfig)
    {
        // 1. Применяем сам эффект (существующая логика)
        // ApplyEffectLogic(effectCfg, sourceUnit, targetUnit);
        
        // 2. Создаём VFX при касте (если есть)
        // if (effectCfg.OnCastVFX.Type != VFXType.None)
        // {
        //     VFXRequestHelper.CreateSingleTargetVFXRequest(
        //         ecb,
        //         effectCfg.OnCastVFX,
        //         sourceUnit,
        //         targetUnit,
        //         sourcePosition,
        //         targetPosition,
        //         targetCell
        //     );
        // }
        
        // 3. Если это projectile, создаём VFX при попадании (отложенный)
        // if (effectCfg.OnHitVFX.Type != VFXType.None)
        // {
        //     float hitTime = CalculateProjectileHitTime(sourcePosition, targetPosition, effectCfg);
        //     
        //     VFXRequestHelper.QueueVFX(
        //         ecb,
        //         sourceUnit,
        //         effectCfg.OnHitVFX,
        //         hitTime,
        //         targetCell,
        //         targetUnit
        //     );
        // }
        
        // 4. Если это persistent эффект (аура/бафф), создаём persistent VFX
        // if (effectCfg.PersistentVFX.Type != VFXType.None)
        // {
        //     VFXRequestHelper.CreateSingleTargetVFXRequest(
        //         ecb,
        //         effectCfg.PersistentVFX,
        //         sourceUnit,
        //         targetUnit,
        //         targetPosition, // Persistent VFX обычно на цели
        //         targetPosition,
        //         targetCell
        //     );
        // }
    }
    
    /// <summary>
    /// Пример вызова VFX для area эффекта
    /// </summary>
    public static void ApplyAreaEffectWithVFX(
        EntityCommandBuffer ecb,
        EffectCfg effectCfg,
        Entity sourceUnit,
        float3 sourcePosition,
        NativeList<int2> affectedCells,
        GridConfig gridConfig)
    {
        // 1. Применяем area эффект ко всем клеткам (существующая логика)
        // foreach (var cell in affectedCells)
        // {
        //     ApplyEffectToCell(effectCfg, cell);
        // }
        
        // 2. Создаём VFX для всей области
        // if (effectCfg.OnCastVFX.Type != VFXType.None)
        // {
        //     VFXRequestHelper.CreateMultiTargetVFXRequest(
        //         ecb,
        //         effectCfg.OnCastVFX,
        //         sourceUnit,
        //         sourcePosition,
        //         affectedCells,
        //         gridConfig
        //     );
        // }
    }
}

/// <summary>
/// ПРИМЕР: Preset конфигурации VFX для типовых способностей
/// </summary>
public static class VFXPresets
{
    /// <summary>
    /// Preset для огненного шара
    /// AimShape: Rect, EffectShape: Cell
    /// </summary>
    public static (VFXConfig onCast, VFXConfig onHit) Fireball()
    {
        var onCast = new VFXConfig
        {
            Type = VFXType.Fireball,
            Timing = VFXTiming.OnCast,
            Origin = VFXOrigin.AtCasterHand,
            Target = VFXTarget.ToCursor,
            Duration = 0.5f,
            Speed = 15f,
            Scale = 1.0f,
            ColorTint = new float4(1, 0.5f, 0, 1),
            FollowTarget = false,
            AttachToUnit = false,
            PersistAfterHit = false
        };
        
        var onHit = new VFXConfig
        {
            Type = VFXType.Explosion,
            Timing = VFXTiming.OnHit,
            Origin = VFXOrigin.AtCursor,
            Target = VFXTarget.ToSelf,
            Duration = 0.8f,
            Delay = 0f,
            Speed = 0f,
            Scale = 2.0f,
            ColorTint = new float4(1, 0.3f, 0, 1),
            FollowTarget = false,
            AttachToUnit = false,
            PersistAfterHit = false
        };
        
        return (onCast, onHit);
    }
    
    /// <summary>
    /// Preset для огненного дыхания
    /// AimShape: FacePoint, EffectShape: Cone
    /// </summary>
    public static VFXConfig FireBreath()
    {
        return new VFXConfig
        {
            Type = VFXType.FireCone,
            Timing = VFXTiming.WhileChanneling,
            Origin = VFXOrigin.AtCasterHead,
            Target = VFXTarget.InDirection,
            Duration = 1.5f,
            Delay = 0f,
            Speed = 0f,
            Scale = 1.5f,
            ColorTint = new float4(1, 0.4f, 0, 1),
            FollowTarget = false,
            AttachToUnit = true, // Привязан к кастеру во время чтения
            PersistAfterHit = false
        };
    }
    
    /// <summary>
    /// Preset для лечения
    /// AimShape: Radius, EffectShape: Cell
    /// </summary>
    public static (VFXConfig beam, VFXConfig glow) Heal()
    {
        var beam = new VFXConfig
        {
            Type = VFXType.HealBeam,
            Timing = VFXTiming.OnCast,
            Origin = VFXOrigin.AtCaster,
            Target = VFXTarget.ToTarget,
            Duration = 0.3f,
            Speed = 20f,
            Scale = 0.3f,
            ColorTint = new float4(0, 1, 0.5f, 1),
            FollowTarget = true, // Луч следует за целью
            AttachToUnit = false,
            PersistAfterHit = false
        };
        
        var glow = new VFXConfig
        {
            Type = VFXType.HealGlow,
            Timing = VFXTiming.OnHeal,
            Origin = VFXOrigin.AtTarget,
            Target = VFXTarget.ToSelf,
            Duration = 1.0f,
            Delay = 0.3f, // Задержка = время полёта луча
            Speed = 0f,
            Scale = 1.5f,
            ColorTint = new float4(0.5f, 1, 0.5f, 1),
            FollowTarget = false,
            AttachToUnit = true, // Glow привязан к цели
            PersistAfterHit = false
        };
        
        return (beam, glow);
    }
    
    /// <summary>
    /// Preset для ядовитого облака
    /// AimShape: Rect, EffectShape: Rect
    /// </summary>
    public static VFXConfig PoisonCloud()
    {
        return new VFXConfig
        {
            Type = VFXType.PoisonCloud,
            Timing = VFXTiming.OnCast,
            Origin = VFXOrigin.AtCursor,
            Target = VFXTarget.ToGround,
            Duration = 3.0f, // Облако висит 3 секунды
            Delay = 0.2f,
            Speed = 0f,
            Scale = 1.0f,
            ColorTint = new float4(0.2f, 0.8f, 0.2f, 0.6f),
            FollowTarget = false,
            AttachToUnit = false,
            PersistAfterHit = true // Облако остаётся после нанесения урона
        };
    }
    
    /// <summary>
    /// Preset для щитовой ауры
    /// AimShape: UnitPoint, EffectShape: Circle
    /// </summary>
    public static VFXConfig ShieldAura()
    {
        return new VFXConfig
        {
            Type = VFXType.ShieldAura,
            Timing = VFXTiming.Persistent,
            Origin = VFXOrigin.AtCaster,
            Target = VFXTarget.ToSelf,
            Duration = -1f, // Infinite - пока не снимут бафф
            Delay = 0f,
            Speed = 0f,
            Scale = 2.0f,
            ColorTint = new float4(0.3f, 0.3f, 1, 0.5f),
            FollowTarget = true,
            AttachToUnit = true,
            PersistAfterHit = true
        };
    }
}
