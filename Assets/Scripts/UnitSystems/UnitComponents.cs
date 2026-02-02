using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Компонент-маркер, что нужно заспаунить юнитов из конфига
public struct SpawnUnitsTag : IComponentData { }




public struct UnitStats : IComponentData
{
    public int Strength;
    public int MoveRange;
}

public struct UnitSize : IComponentData { public int2 Value; }

public struct MoveCommand : IComponentData
{
    public bool IsMoving;
    public float3 TargetPosition;
    public float MoveSpeed;
}

public enum UnitLayer
{
    Underground, // Подземный (кроты, черви)
    Ground,      // Наземный (стандарт)
    Sky          // Воздушный (игнорирует наземные препятствия)
}

// Компонент для юнита, определяющий его слой
public struct UnitLayerData : IComponentData
{
    public UnitLayer Value;
}

public struct UnitFacing : IComponentData
{
    public int2 Value;

}
public enum UnitFacingMode
{
    Fixed,  // Не поворачивается (сохраняет начальный поворот)
    OnlyX,  // Поворачивается только влево/вправо (для 2D или сайд-скроллеров)
    Free    // Свободный поворот в сторону движения
}



public struct StatusEffect : IBufferElementData
{
    public EffectType Type;
    public StatusEffectType StatusType;
    public Entity SourceUnit;
    public Entity TargetUnit;
    public int Power;
    public int Duration;
    public int Charges;
    public Entity TargetCard;
    public FixedString128Bytes Description;
    public bool IsVisible;
}
public struct UnitCardBuffer : IBufferElementData
{
    public Entity CardEntity;
}




[Serializable]
public class UnitCfg
{
    public int Id;
    public string Class;
    public string Faction;
    public int Strength;
    public int Reaction;
    public int BaseMaxHP;
    public int BaseArmor;
    public int BaseMaxSP;
    public int BaseRenewAP;
    public int BaseRenewCards;
    public int BaseHandSize;
    public int PositionX;
    public int PositionZ;
    public int SizeX;
    public int SizeZ;
    public string Layer;
    // Новые поля для поворота
    public int FacingX; // -1, 0, 1
    public int FacingZ; // -1, 0, 1
    public string UnitPrefab;
}

public struct UnitIdComponent : IComponentData
{
    public int UnitId;
}

public struct UnitBuffer : IBufferElementData
{
    public Entity UnitEntity;
}

public struct UnitSPStats : IComponentData
{
    public int BaseMaxSP;
    public int CurrMaxSP;
    public int CurrSP;
}

public struct UnitHPStats : IComponentData
{
    public int BaseMaxHP;
    public int CurrMaxHP;
    public int CurrHP;
    public int BaseArmor;
    public int CurrArmor;
    public int CurrBlock;
    public int CurrDodge;
    public int CurrPoison;
}
/*
public struct UnitStats : IComponentData
{
    public int Strength;
    public int Reaction;
    public int MoveRange;
}
*/
public struct UnitCardStats : IComponentData
{
    public int BaseRenewCards;
    public int CurrRenewCards;
    public int BaseHandSize;
    public int CurrHandSize;
}
public struct UnitAPStats : IComponentData
{
    public int BaseRenewAP;
    public int CurrRenewAP;
    public int CurrAP;
    public bool ResetAPonStartTurn;
}


public enum FactionType : byte
{
    Hero,
    Enemy
}
public struct UnitFaction : IComponentData
{
    public FactionType Faction;
}

// Состояние хода юнита
public enum TurnState
{
    waitTurn,
    startTurn,
    inTurn,
    endTurn,
    outTurn
}

// Компонент, определяющий состояние хода конкретного юнита
public struct c_TurnState : IComponentData
{
    public TurnState State;
}

public struct UnitReaction
{
    public Entity Entity;
    public int Reaction;
}
public struct ReactionDescendingComparer : IComparer<UnitReaction>
{
    public int Compare(UnitReaction a, UnitReaction b)
    {
        // по убыванию реакции
        return b.Reaction.CompareTo(a.Reaction);
    }
}
public struct NeedsUnitIndicatorTag : IComponentData { }
public struct UnitIndicatorRegisteredTag : IComponentData { }

public struct DeadTag : IComponentData { }
