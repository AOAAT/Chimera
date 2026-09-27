using UnityEngine;
using UnityEngine.EventSystems;

public class RTSCameraMover : MonoBehaviour
{
    public float KeyboardSpeed = 30f;
    public float DragSensitivity = 1f;
    public bool ClampToGrid = true;
    private Vector3 lastMousePos;
    private Camera viewCamera;

    // 移动相机父容器；震屏只修改子相机的局部偏移。
    private void Awake() => viewCamera = GetComponentInChildren<Camera>();

    private void LateUpdate()
    {
        if (Time.timeScale == 0f) return;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        {
            var selected = EventSystem.current.currentSelectedGameObject;
            var tmpInput = selected.GetComponent<TMPro.TMP_InputField>();
            var inputField = selected.GetComponent<UnityEngine.UI.InputField>();
            if ((tmpInput != null && tmpInput.isFocused) ||
                (inputField != null && inputField.isFocused)) return;
        }
        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x--;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x++;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.y--;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.y++;
        Pan(input.normalized * KeyboardSpeed * Time.unscaledDeltaTime);
        if (Input.GetMouseButtonDown(2)) lastMousePos = Input.mousePosition;
        if (Input.GetMouseButton(2) && viewCamera != null)
        {
            PanScreenDelta(lastMousePos - Input.mousePosition);
            lastMousePos = Input.mousePosition;
        }
    }

    public void PanScreenDelta(Vector2 screenDelta)
    {
        if (viewCamera == null) return;
        Vector3 delta = viewCamera.ScreenToWorldPoint(screenDelta) - viewCamera.ScreenToWorldPoint(Vector3.zero);
        Pan(new Vector2(delta.x, delta.y) * DragSensitivity);
    }

    public void Pan(Vector2 worldDelta)
    {
        if (viewCamera == null) return;
        Vector3 currentPosition = viewCamera.transform.position;
        Vector3 position = currentPosition + new Vector3(worldDelta.x, worldDelta.y, 0);
        if (ClampToGrid && RTSGridSystem.Instance != null)
        {
            Bounds bounds = RTSGridSystem.Instance.WorldBounds;
            position.x = Mathf.Clamp(position.x, bounds.min.x, bounds.max.x);
            position.y = Mathf.Clamp(position.y, bounds.min.y, bounds.max.y);
        }
        transform.position += position - currentPosition;
    }
}
