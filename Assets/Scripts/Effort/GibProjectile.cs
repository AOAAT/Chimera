// --- START OF FILE GibProjectile.cs ---
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class GibProjectile : MonoBehaviour
{
    [Header("=== 碎块物理参数 ===")]
    public float MinEjectSpeed = 5f;
    public float MaxEjectSpeed = 15f;
    public float Deceleration = 5f; // 摩擦力，让它很快停下来
    public float RotationSpeedMultiplier = 720f;

    [Header("=== 落地表现 ===")]
    public Color GroundedColor = new Color(0.6f, 0.6f, 0.6f, 1f); // 停下后变暗，融入背景

    [Tooltip("选填：在飞溅过程中，是否要在地上留下血迹/机油的贴图预制体？")]
    public GameObject BloodDecalPrefab;
    public float DecalSpawnInterval = 0.1f; // 多久滴一滴血

    private Rigidbody2D rb;
    private SpriteRenderer sr;
    private float currentSpeed;
    private Vector2 moveDirection;
    private float rotationSpeed;
    private float decalTimer;
    private bool hasStopped = false;

    public void Eject(Vector2 direction)
    {
        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        // 彻底关闭物理碰撞，纯靠代码计算位移，极大地节省性能！
        rb.isKinematic = true;
        rb.simulated = false;

        // 随机一个初速度和旋转速度
        currentSpeed = Random.Range(MinEjectSpeed, MaxEjectSpeed);

        // 给喷射方向加一点扇形随机偏移 (比如 ±30度)，看起来更像爆炸
        float randomAngle = Random.Range(-30f, 30f);
        moveDirection = Quaternion.Euler(0, 0, randomAngle) * direction.normalized;

        rotationSpeed = Random.Range(-RotationSpeedMultiplier, RotationSpeedMultiplier);

        // 随机稍微缩放一下碎块，让同样的贴图看起来大小不一
        float randomScale = Random.Range(0.6f, 1.2f);
        transform.localScale = new Vector3(randomScale, randomScale, 1f);

        // 确保碎块在尸体之上，但在活着的单位之下
        sr.sortingLayerName = "Floor"; // 建议你在 Unity 里建一个专用的 Floor 图层
        sr.sortingOrder = Random.Range(10, 100);
    }

    private void Update()
    {
        if (hasStopped) return;

        // 1. 位移与减速
        transform.position += (Vector3)(moveDirection * currentSpeed * Time.deltaTime);
        currentSpeed -= Deceleration * Time.deltaTime;

        // 2. 旋转
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);

        // 3. 滴血逻辑
        if (BloodDecalPrefab != null && currentSpeed > 0)
        {
            decalTimer -= Time.deltaTime;
            if (decalTimer <= 0)
            {
                SpawnDecal();
                decalTimer = DecalSpawnInterval;
            }
        }

        // 4. 彻底停下，变成永久的环境装饰物
        if (currentSpeed <= 0)
        {
            currentSpeed = 0;
            hasStopped = true;
            sr.color = GroundedColor; // 变暗

            // 停下时在原地再爆一滩大血迹
            if (BloodDecalPrefab != null) SpawnDecal(true);

            // 移除脚本以节省 CPU 性能，它现在只是一张静态贴图了！
            Destroy(this);
        }
    }

    private void SpawnDecal(bool isFinal = false)
    {
        GameObject decal = Instantiate(BloodDecalPrefab, transform.position, Quaternion.identity);

        // 随机旋转血迹贴图
        decal.transform.rotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));

        // 如果是最后停下那一下，血迹大一点
        float scale = isFinal ? Random.Range(0.8f, 1.5f) : Random.Range(0.3f, 0.6f);
        decal.transform.localScale = new Vector3(scale, scale, 1f);
    }
}