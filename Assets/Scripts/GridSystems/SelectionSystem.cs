using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems; // Для проверки клика по UI

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class SelectionSystem : SystemBase
{
    private InputSystem_Actions _inputActions;

    protected override void OnCreate()
    {
        _inputActions = new InputSystem_Actions();
        _inputActions.Game.Enable();
        RequireForUpdate<PhysicsWorldSingleton>();
    }

    protected override void OnDestroy()
    {
        _inputActions.Game.Disable();
        _inputActions.Dispose();
    }

    protected override void OnUpdate()
    {
        // 0. Ensure SelectionState Exists
        if (!SystemAPI.HasSingleton<ActiveUnitComponent>())
        {
            var e = EntityManager.CreateEntity();
            EntityManager.AddComponentData(e, new ActiveUnitComponent { Unit = Entity.Null, Mode = InteractionMode.None });
            return;
        }

        var selectionState = SystemAPI.GetSingleton<ActiveUnitComponent>();
        bool stateChanged = false;

        // 1. HANDLE UI REQUESTS (Move / Effect buttons)
        if (SystemAPI.TryGetSingletonEntity<UIActionRequest>(out Entity reqEntity))
        {
            var req = EntityManager.GetComponentData<UIActionRequest>(reqEntity);
            if (req.IsPending)
            {
                // Реагируем на кнопки только если юнит уже выбран
                if (selectionState.Unit != Entity.Null && EntityManager.Exists(selectionState.Unit))
                {
                    if (selectionState.Mode != req.RequestedMode)
                    {
                        selectionState.Mode = req.RequestedMode;
                        stateChanged = true;
                        Debug.Log($"[SelectionSystem] Mode switched to: {req.RequestedMode} via UI");
                    }
                }
                else
                {
                    Debug.LogWarning("[SelectionSystem] Cannot switch mode: No unit selected!");
                }

                // Reset pending flag
                req.IsPending = false;
                EntityManager.SetComponentData(reqEntity, req);
            }
        }

        // 2. CANCEL (Right Click or Escape)
        // Сбрасывает выделение или текущий режим
        if (_inputActions.Game.Cancel.WasPerformedThisFrame())
        {
            if (selectionState.Unit != Entity.Null)
            {
                // Если был активен режим - сбрасываем только режим
                if (selectionState.Mode != InteractionMode.None)
                {
                    selectionState.Mode = InteractionMode.None;
                    Debug.Log("[SelectionSystem] Mode cancelled (Unit still selected)");
                }
                // Если режима не было - сбрасываем юнита
                else
                {
                    selectionState.Unit = Entity.Null;
                    Debug.Log("[SelectionSystem] Unit deselected");
                }
                stateChanged = true;
            }
        }

        // 3. KEYBOARD SHORTCUTS (Optional fallback)
        /*
        if (selectionState.SelectedUnit != Entity.Null)
        {
            if (Keyboard.current.aKey.wasPressedThisFrame) 
            { 
                selectionState.Mode = InteractionMode.Effect; 
                stateChanged = true; 
            }
            else if (Keyboard.current.mKey.wasPressedThisFrame) 
            { 
                selectionState.Mode = InteractionMode.Move; 
                stateChanged = true; 
            }
        }
        */

        // 4. UNIT SELECTION (Left Click Raycast)
        if (Camera.main != null && _inputActions.Game.Click.WasPerformedThisFrame())
        {
            // Простейшая защита от клика сквозь UI (требует EventSystem на сцене)
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                // Click handled by UI
            }
            else
            {
                Vector2 mousePos = _inputActions.Game.Position.ReadValue<Vector2>();
                UnityEngine.Ray unityRay = Camera.main.ScreenPointToRay(mousePos);
                var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().CollisionWorld;
                var rayInput = new RaycastInput
                {
                    Start = unityRay.origin,
                    End = unityRay.origin + unityRay.direction * 1000f,
                    Filter = CollisionFilter.Default
                };

                if (physicsWorld.CastRay(rayInput, out var hit))
                {
                    // Проверяем, кликнули ли мы по юниту (есть UnitStats или UnitIdComponent)
                    // Важно: на юните должен быть PhysicsShape (Collider), чтобы рейкаст попал
                    if (EntityManager.HasComponent<UnitStats>(hit.Entity))
                    {
                        if (hit.Entity != selectionState.Unit)
                        {
                            selectionState.Unit = hit.Entity;

                            // ВАЖНО: По умолчанию режим None (ждем нажатия кнопки в UI)
                            selectionState.Mode = InteractionMode.None;

                            stateChanged = true;
                            Debug.Log($"[SelectionSystem] Unit Selected: {hit.Entity.Index}. Waiting for command...");
                        }
                    }
                    // Опционально: Клик в пустоту снимает выделение?
                    // else if (selectionState.SelectedUnit != Entity.Null)
                    // {
                    //    selectionState.SelectedUnit = Entity.Null;
                    //    selectionState.Mode = InteractionMode.None;
                    //    stateChanged = true;
                    // }
                }
            }
        }

        if (stateChanged)
        {
            SystemAPI.SetSingleton(selectionState);
        }
    }
}
