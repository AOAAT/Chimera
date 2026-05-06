using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class GibProjectile : MonoBehaviour
{
    [Header("=== 碎块物理参数 ===")]
    public float MinEjectSpeed = 5f;
    public float MaxEjectSpeed = 15f;
    public float Deceleration = 5f;
    public float RotationSpeedMultiplier = 720f;

    [Header("=== 落地表现 ===")]
    public Color GroundedColor = new Color(0.6f, 0.6f, 0.6f, 1f);
    private Color originalColor;

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private float currentSpeed;
    private Vector2 moveDirection;
    private float rotationSpeed;
    private bool hasStopped = false;
    private GameObject mySourcePrefab;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();
        originalColor = sr.color;
    }

    // 👇【核心修复】：从对象池复用时，重置“已停下”标志位和透明度
    private void OnEnable()
    {
        hasStopped = false;
        if (sr != null) sr.color = originalColor;
    }

    public void Eject(Vector2 direction, GameObject prefabRef)
    {
        mySourcePrefab = prefabRef;
        currentSpeed = Random.Range(MinEjectSpeed, MaxEjectSpeed);
        float randomAngle = Random.Range(-30f, 30f);
        moveDirection = Quaternion.Euler(0, 0, randomAngle) * direction.normalized;
        rotationSpeed = Random.Range(-RotationSpeedMultiplier, RotationSpeedMultiplier);

        sr.sortingLayerName = "Floor";
    }

    private void Update()
    {
        if (hasStopped) return;

        transform.position += (Vector3)(moveDirection * currentSpeed * Time.deltaTime);
        currentSpeed -= Deceleration * Time.deltaTime;
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

        if (currentSpeed <= 0)
        {
            currentSpeed = 0;
            hasStopped = true;
            sr.color = GroundedColor;

            // 归还对象池
            SimplePool.Despawn(mySourcePrefab, this.gameObject);
        }
    }
}