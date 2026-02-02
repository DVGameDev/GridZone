using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

public class ActionBarController : MonoBehaviour
{
    private UIDocument _uiDocument;
    private Button _moveButton;
    private Button _effectButton;

    void OnEnable()
    {
        _uiDocument = GetComponent<UIDocument>();
        var root = _uiDocument.rootVisualElement;

        _moveButton = root.Q<Button>("MoveButton");
        _effectButton = root.Q<Button>("EffectButton");

        _moveButton.clicked += OnMoveClicked;
        _effectButton.clicked += OnEffectClicked;
    }

    void OnDisable()
    {
        if (_moveButton != null) _moveButton.clicked -= OnMoveClicked;
        if (_effectButton != null) _effectButton.clicked -= OnEffectClicked;
    }

    private void OnMoveClicked()
    {
        SendActionRequest(InteractionMode.Move);
    }

    private void OnEffectClicked()
    {
        SendActionRequest(InteractionMode.Effect);
    }

    private void SendActionRequest(InteractionMode mode)
    {
        var world = World.DefaultGameObjectInjectionWorld;
        if (world == null) return;

        var em = world.EntityManager;

        // Ищем или создаем синглтон запроса
        var query = em.CreateEntityQuery(typeof(UIActionRequest));
        Entity requestEntity;

        if (query.CalculateEntityCount() == 0)
        {
            requestEntity = em.CreateEntity(typeof(UIActionRequest));
        }
        else
        {
            requestEntity = query.GetSingletonEntity();
        }

        em.SetComponentData(requestEntity, new UIActionRequest
        {
            RequestedMode = mode,
            IsPending = true
        });

        Debug.Log($"[UI] Requested Mode: {mode}");
    }
}
