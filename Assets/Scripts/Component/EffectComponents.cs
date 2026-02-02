using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;


public struct EffectCommandBuffer : IBufferElementData
{
    public EffectCommand Command;    
}

public struct ActiveUnitComponent : IComponentData
{
    public Entity Unit;
    public InteractionMode Mode;
    public int2 HoveredGridCell; // <--- ADDED: Текущая клетка под мышкой (для превью эффекта)
}

public struct EffectShapeData : IComponentData
{
    public AimShapeConfig AimShape;
    public EffectShapeConfig EffectShape;
    public ImpactOrigin OriginType;
}

public struct UnitEffectData : IComponentData
{
    // ТОЛЬКО ссылка на активный эффект
    public Entity EffectEntity;
}

[Serializable]
public struct EffectShapeConfig
{
    public EffectShapeType Type;
    public int SizeX;      // Длина / Радиус
    public int SizeZ;      // Ширина / Угол / Внутренний радиус
    public EffectShapeLevel EffectLevel; // 
}

[Serializable]
public struct AimShapeConfig
{
    public AimShapeType Type;
    public int SizeX;      // Длина / Радиус
    public int SizeZ;      // Ширина / Угол / Внутренний радиус
    public int Offset; // Для задания минимальной дистанции
}

public enum EffectShapeType
{
    Cell,      // Одиночная клетка    
    Cone,       // Конус (треугольник)
    Rect,       // Прямоугольник/Квадрат
    Cross,      // Крест (как у бомбермена)
    Circle,     // Круг (c центром)
    Ring        // Кольцо (вокруг центра)
}
public enum AimShapeType
{
    UnitPoint,      
    FacePoint,
    Rect,
    Cone,
    Radius,
    Ring,
    HalfRing
}

public enum EffectShapeLevel
{
    Ground,
    Sky,
    Underground,
    SkyGround,
    AllGround,
    All
}

public enum AimingMode
{
    Free,   // Курсор в любом месте радиуса (круг/квадрат)
    Direct, // Только 4 стороны (крест от юнита)
    Fixed   // Нельзя вращать (всегда перед собой)
}
public enum ImpactOrigin
{
    AtCursor, // Центр формы в точке КУКСОРА (Фаербол, Град стрел)
    AtUnit    // Центр формы в ЮНИТЕ, повернут к курсору (Дыхание дракона, Выстрел из ружья)
}

public enum EffectType : byte
{
    None,
    MeleeDamage,
    Heal,
    Growth,
    Ilness,
    AddBlock,
    RemoveBlock,
    GetArmor,
    LoseArmor,
    RenewCards,
    DrawCards,
    DiscardCards,
    DiscardAllCards,
    AddAP,
    RenewAP,
    SubAP,
    ResetAPoff,
    ResetAPon,
    AddSP,
    Dodge,
    Vulnerability,
    Invulnerability,
    InsteadOfDeadLeft1HP,
    Fury

}

public struct EffectCommand : IBufferElementData
{
    public EffectType Type;
    public StatusEffectType StatusType;
    public Entity SourceUnit;
    public Entity TargetUnit;
    public Entity TargetCard;
    public int Power;
    public int Repeat;
    public int Duration;
    public int Charges;
    public FixedString128Bytes Description;
    public bool IsVisible;
}

[Serializable]
public struct EffectCfg
{
    public int UnitID;
    public int EffectID;
    public EffectType EffectType;
    public StatusEffectType StatusType;
    public int Power;
    public int Repeat;
    public int Duration;
    public int Charges;
    public EffectTargetType TargetType;
    public string Description;
    public bool IsVisible;
    public AimShapeType AimShapeType;
    public int ASizeX;
    public int ASizeZ;
    public int Offset;
    public EffectShapeType EffectShapeType;
    public int ESizeX;
    public int ESizeZ;
    public EffectShapeLevel EffectShapeLevel;    
}





public struct EffectStatComponent : IComponentData
{
    public FixedString64Bytes Name;
    public StatusEffectType StatusType;
    public int Power;
    public int Repeat;
    public int Duration;
    public int Charges;
    public EffectTargetType TargetType;
    public FixedString128Bytes Description;
    public bool IsVisible;
}
public enum EffectTargetType : byte
{
    Self,
    All,
    Heroes,
    Enemies,
    Allies,
    SingleAlly,
    SingleEnemy
}

public struct EffectCommandBufferTag : IComponentData
{ }





public enum StatusEffectType : byte
{
    Momentum,    // Моментальный
    EveryTurn,   // Каждый ход
    Check,       // Проверка при условии
    Call,        // Временное изменение (при наложении)
    Recall       // Возврат изменения (при снятии)
}