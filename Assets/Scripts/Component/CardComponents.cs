using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;



public struct PlayCardRequest : IComponentData
{
    public Entity Unit;
    public Entity Card;
}

public struct CardEffectBuffer : IBufferElementData
{
    public Entity EffectEntity;
}


public struct CardConfig
{
    public int UnitId;
    public int CardId;
    public FixedString64Bytes Name;
    public int Cost;
    public CardUseType UseType;    
    public FixedString128Bytes Description;
}

public struct CardStatComponent : IComponentData
{
    public int CardId;
    public FixedString64Bytes Name;    
    public int APCost;    
    public CardUseType UseType;
    public FixedString128Bytes BaseDescription;
}

public enum CardPileState : byte
{
    DrawPile,
    HandPile,
    DiscardPile,
    ExilePile
}
public struct CardPileStateComponent : IComponentData
{
    public CardPileState State;
}
public enum CardUseType : byte
{
    Magnet,
    Discard,
    Ethereal,
    Exile
}


public struct PileChangedTag : IComponentData { }

public struct CardLoadedTag : IComponentData { }
