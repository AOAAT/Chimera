using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ResidentEntity : MonoBehaviour
{
    [Header("=== 绑定的数据 ===")]
    public ResidentData MyData;

    [Header("=== 物理与移动参数 ===")]
    public float MoveSpeed = 3.5f;
    private Rigidbody2D rb;
    private Vector2? targetPosition = null;

    [Header("=== UI 与选中反馈 ===")]
    public GameObject SelectionCircle; // 居民脚下的小光圈

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        SetupPhysics();
    }

    private void SetupPhysics()
    {
        // 1. 设置 RTS 物理规格
        gameObject.layer = LayerMask.NameToLayer("Resident");
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.drag = 8f; // 居民体量小，停步要快，防止滑行感

        // 2. 精细碰撞核：0.2m 半径
        CircleCollider2D col = GetComponent<CircleCollider2D>();
        col.radius = 0.2f;
        col.isTrigger = false;
    }

    public void Initialize(ResidentData data)
    {
        MyData = data;
        gameObject.name = $"Resident_{data.ResidentName}";
        SetSelected(false);
    }

    public void SetDestination(Vector2 worldPos)
    {
        if (targetPosition != null)
        {
            // 未来可以触发一个“转身”或“加速”的微操
        }

        targetPosition = worldPos;
    }

    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (targetPosition == null) return;

        float dist = Vector2.Distance(transform.position, targetPosition.Value);
        if (dist < 0.1f)
        {
            rb.velocity = Vector2.zero;
            targetPosition = null;
            return;
        }

        Vector2 dir = (targetPosition.Value - (Vector2)transform.position).normalized;
        rb.velocity = dir * MoveSpeed;
    }

    // --- 选中状态控制 ---
    public void SetSelected(bool isSelected)
    {
        if (SelectionCircle != null) SelectionCircle.SetActive(isSelected);
    }
}