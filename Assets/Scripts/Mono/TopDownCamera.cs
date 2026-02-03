using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Камера сверху (Top-Down) с управлением WSAD и зумом колесиком мыши.
/// </summary>
public class TopDownCamera : MonoBehaviour
{
    [Header("Параметры камеры")]
    public Vector3 Target = Vector3.zero;    // Центральная точка
    public float Height = 20f;               // Высота над центром
    public float MoveSpeed = 10f;            // Скорость движения по X/Z
    public float ZoomSpeed = 10f;            // Скорость зума колесиком
    public float MinHeight = 5f;             // Минимальная высота
    public float MaxHeight = 50f;            // Максимальная высота

    private Keyboard keyboard;
    private Mouse mouse;

    void Start()
    {
        keyboard = Keyboard.current;
        mouse = Mouse.current;

        UpdateCameraPosition();
    }

    void Update()
    {
        HandleMovement();
        HandleZoom();
        UpdateCameraPosition();
    }

    void HandleMovement()
    {
        if (keyboard == null) return;

        Vector3 move = Vector3.zero;

        if (keyboard.wKey.isPressed) move.z += 1f;
        if (keyboard.sKey.isPressed) move.z -= 1f;
        if (keyboard.aKey.isPressed) move.x -= 1f;
        if (keyboard.dKey.isPressed) move.x += 1f;

        move = move.normalized * MoveSpeed * Time.deltaTime;

        Target += move;
    }

    void HandleZoom()
    {
        if (mouse == null) return;

        Vector2 scrollDelta = mouse.scroll.ReadValue();
        Height -= scrollDelta.y * ZoomSpeed * 0.1f;
        Height = Mathf.Clamp(Height, MinHeight, MaxHeight);
    }

    void UpdateCameraPosition()
    {
        transform.position = new Vector3(Target.x, Height, Target.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // смотри строго вниз
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(Target, 0.5f);
        Gizmos.DrawLine(transform.position, Target);
    }
}
