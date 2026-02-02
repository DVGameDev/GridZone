
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Ссылка на prefab визуала
/// </summary>
public struct AnimationSync : IComponentData
{
    public float WalkCycleLength; // Длина одного цикла ходьбы (в секундах)
}/// 

public class VisualPrefab : IComponentData
{
    public GameObject Value;
}

/// <summary>
/// Тег: нужно создать визуал
/// </summary>
public struct NeedsVisualInstantiation : IComponentData { }

/// <summary>
/// Ссылка на созданный GameObject
/// </summary>
public class VisualGameObject : IComponentData, ICleanupComponentData
{
    public GameObject Value;
    public Animator Animator;
}

/// <summary>
/// Состояние анимации
/// </summary>
public struct AnimationState : IComponentData
{
    public bool IsWalking;
}
