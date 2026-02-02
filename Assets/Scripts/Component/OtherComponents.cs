using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;




public enum BattleState
{
    startBattle,
    inBattle,
    endBattle
}
public struct c_BattleState : IComponentData
{
    public BattleState State;
}
public enum RoundState
{
    startRound,
    inRound,
    endRound
}
public struct c_RoundState : IComponentData
{
    public RoundState State;
    public int RoundIndex;
    public int CurrentTurnIndex;
}
public struct RoundActive : IComponentData
{
}

public struct wf_calcSequence : IComponentData
{
}

public struct TurnOrderElement : IBufferElementData
{
    public Entity Unit;
    public int Order; // номер хода в раунде
}
public struct UIReadyTag : IComponentData { }

public struct BattleReadyTag : IComponentData { }

public struct EndTurnRequest : IComponentData
{
}
public struct RoundSystemCreated : IComponentData
{
}