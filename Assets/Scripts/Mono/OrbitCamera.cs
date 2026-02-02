using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Орбитальная камера: вращение вокруг центра (A/D), зум (колесико)
/// Работает с новым Input System
/// </summary>
public class OrbitCamera : MonoBehaviour
{
    [Header("Настройки орбиты")]
    public Vector3 Target = Vector3.zero;
    public float Distance = 20f;
    public float Height = 15f;
    public float Angle = 45f;

    [Header("Управление")]
    public float RotationSpeed = 50f;
    public float ZoomSpeed = 5f;
    public float MinDistance = 5f;
    public float MaxDistance = 50f;

    private float currentRotation = 0f;

    // Input System references
    private Keyboard keyboard;
    private Mouse mouse;

    void Start()
    {
        currentRotation = -45f;

        // Получаем ссылки на устройства ввода
        keyboard = Keyboard.current;
        mouse = Mouse.current;

        UpdateCameraPosition();
    }

    void Update()
    {
        HandleRotation();
        HandleZoom();
        UpdateCameraPosition();
    }

    void HandleRotation()
    {
        if (keyboard == null) return;

        float horizontal = 0f;

        // A - влево, D - вправо
        if (keyboard.aKey.isPressed)
            horizontal = -1f;
        if (keyboard.dKey.isPressed)
            horizontal = 1f;

        currentRotation += horizontal * RotationSpeed * Time.deltaTime;
    }

    void HandleZoom()
    {
        if (mouse == null) return;

        // Колесико мыши
        Vector2 scrollDelta = mouse.scroll.ReadValue();
        float scroll = scrollDelta.y * 0.01f; // Нормализуем

        Distance -= scroll * ZoomSpeed;
        Distance = Mathf.Clamp(Distance, MinDistance, MaxDistance);
    }

    void UpdateCameraPosition()
    {
        float radians = currentRotation * Mathf.Deg2Rad;
        float angleRadians = Angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Sin(radians) * Distance * Mathf.Cos(angleRadians),
            Height,
            Mathf.Cos(radians) * Distance * Mathf.Cos(angleRadians)
        );

        transform.position = Target + offset;
        transform.LookAt(Target);
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Target, 0.5f);
        Gizmos.DrawLine(transform.position, Target);
    }
}
