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
    public float MoveSpeed = 10f;

    [Header("Настройки камеры")]
    [Tooltip("Увеличить для отображения всей сетки при виде сверху")]
    public float FarClipPlane = 1000f;
    [Tooltip("Использовать ортографическую проекцию (рекомендуется для вида сверху)")]
    public bool UseOrthographic = false;
    [Tooltip("Размер ортографической камеры")]
    public float OrthographicSize = 35f;

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
        
        // 🔥 ИСПРАВЛЕНИЕ: настройка камеры для корректного отображения всей сетки
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            // Увеличиваем дальность отрисовки
            cam.farClipPlane = FarClipPlane;
            
            // Опционально: переключаем на ортографическую проекцию
            if (UseOrthographic)
            {
                cam.orthographic = true;
                cam.orthographicSize = OrthographicSize;
            }
            
            Debug.Log($"[OrbitCamera] Camera setup: farClip={cam.farClipPlane}, orthographic={cam.orthographic}");
        }

        UpdateCameraPosition();
    }

    void Update()
    {
        HandleRotation();
        HandleZoom();
        UpdateCameraPosition();
        HandleMovement();
        
    }

    void HandleRotation()
    {
        if (keyboard == null) return;

        float horizontal = 0f;

        // A - влево, D - вправо
        if (keyboard.qKey.isPressed)
            horizontal = -1f;
        if (keyboard.eKey.isPressed)
            horizontal = 1f;

        currentRotation += horizontal * RotationSpeed * Time.deltaTime;
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

        // Колесико мыши
        Vector2 scrollDelta = mouse.scroll.ReadValue();
        float scroll = scrollDelta.y * 0.01f; // Нормализуем

        Distance -= scroll * ZoomSpeed;
        Distance = Mathf.Clamp(Distance, MinDistance, MaxDistance);
    }

    void UpdateCameraPosition()
    {
        /*
        float radians = currentRotation * Mathf.Deg2Rad;
        float angleRadians = Angle * Mathf.Deg2Rad;

        Vector3 offset = new Vector3(
            Mathf.Sin(radians) * Distance * Mathf.Cos(angleRadians),
            Height,
            Mathf.Cos(radians) * Distance * Mathf.Cos(angleRadians)
        );

        transform.position = Target + offset;
        transform.LookAt(Target);
        */
        transform.position = new Vector3(Target.x, Height, Target.z);
        transform.rotation = Quaternion.Euler(90f, 0f, 0f); // смотри строго вниз
        
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Target, 0.5f);
        Gizmos.DrawLine(transform.position, Target);
    }
}
