using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class ResidentEntity : MonoBehaviour
{
    [Header("=== 绑定的数据 ===")]
    public ResidentData MyData;

    [Header("=== 物理与移动参数 ===")]
    public float MoveSpeed = 3.5f;
    private Rigidbody2D rb;


    [Header("=== UI 与选中反馈 ===")]
    public GameObject SelectionCircle; // 居民脚下的小光圈


    private List<Vector3> currentPath = null;
    private int pathIndex = 0;
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        SetupPhysics();
    }

    private void SetupPhysics()
    {
        gameObject.layer = LayerMask.NameToLayer("Resident");
        rb = GetComponent<Rigidbody2D>();

        // --- 👇 同步注入物理材质 ---
        PhysicsMaterial2D slippery = Resources.Load<PhysicsMaterial2D>("Slippery_Material");
        if (slippery != null) rb.sharedMaterial = slippery;
        // ----------------------------

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.drag = 8f;

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

    public void SetDestination(Vector2 worldPos) // 或者 SetManualMovePoint
    {
        // 🌟 核心：在计算新路径前，立即切断当前所有物理惯性
        if (rb != null) rb.velocity = Vector2.zero;

        currentPath = GridPathfinder.FindPath(transform.position, worldPos);
        pathIndex = 0;

        // 如果路径只有1个点（就在脚下），直接清理掉，防止原地抽搐
        if (currentPath != null && currentPath.Count <= 1) currentPath = null;
    }
    private void Update()
    {
        HandleMovement();
    }

    private void HandleMovement()
    {
        if (currentPath == null || pathIndex >= currentPath.Count)
        {
            rb.velocity = Vector2.zero;
            return;
        }

        Vector3 targetPos = currentPath[pathIndex];
        float dist = Vector2.Distance(transform.position, targetPos);

        if (dist < 0.2f)
        {
            pathIndex++;
        }
        else
        {
            Vector2 dir = (targetPos - transform.position).normalized;
            rb.velocity = dir * MoveSpeed;

            // 🌟 视觉平滑：根据移动方向水平翻转 Sprite
            if (Mathf.Abs(dir.x) > 0.01f)
            {
                float targetScaleX = dir.x > 0 ? 1f : -1f;
                Transform visual = transform.Find("Visual_Sprite");
                if (visual != null)
                    visual.localScale = new Vector3(targetScaleX, 1, 1);
            }
        }
    }
    // --- 选中状态控制 ---
    public void SetSelected(bool isSelected)
    {
        if (SelectionCircle != null) SelectionCircle.SetActive(isSelected);
    }
}