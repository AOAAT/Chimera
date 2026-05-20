using UnityEngine;

public class RTSCameraMover : MonoBehaviour
{
    [Header("=== 移动灵敏度 ===")]
    public float KeyboardSpeed = 30f;
    public float DragSensitivity = 2.5f;

    [Header("=== 边界限制 (格) ===")]
    public float MinX = -5f;
    public float MaxX = 105f;

    private Vector3 lastMousePos;
    private float currentX;

    private void Start()
    {
        currentX = transform.position.x;
        // 强制初始位置，确保 Z 轴在 -10
        transform.position = new Vector3(currentX, 0, -10f);
    }

    private void LateUpdate()
    {
        float moveInput = 0;

        // 1. 键盘 A/D
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveInput -= KeyboardSpeed;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveInput += KeyboardSpeed;

        // 2. 鼠标中键拖拽 (Middle Mouse = 2)
        if (Input.GetMouseButtonDown(2))
        {
            lastMousePos = Input.mousePosition;
        }

        if (Input.GetMouseButton(2))
        {
            Vector3 delta = lastMousePos - Input.mousePosition;
            moveInput += delta.x * DragSensitivity; // 将鼠标位移转化为移动增量
            lastMousePos = Input.mousePosition;
        }

        // 3. 应用位移与边界锁定
        currentX += moveInput * Time.unscaledDeltaTime;
        currentX = Mathf.Clamp(currentX, MinX, MaxX);

        // 4. 最终坐标锁定
        transform.position = new Vector3(currentX, 0, -10f);
    }
}