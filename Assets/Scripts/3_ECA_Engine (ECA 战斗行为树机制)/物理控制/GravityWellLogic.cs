using UnityEngine;
using System.Linq;

public class GravityWellLogic : MonoBehaviour
{
    [Header("=== 引力核心参数 ===")]
    public float Duration = 3f;
    public float PullRadius = 8f;
    public float PullForce = 25f;    // 持续牵引力
    public float TickRate = 0.02f;   // 设置为 0.02s (每帧拉取一次，实现丝滑吸附)

    private float timer = 0f;
    private float tickTimer = 0f;

    public void Initialize(float duration, float radius, float force)
    {
        this.Duration = duration;
        this.PullRadius = radius;
        this.PullForce = force;

        // 视觉缩放：让黑洞特效的大小也跟随射程缩放
        float distMult = CombatSandbox.Instance != null ? CombatSandbox.Instance.DistanceMultiplier : 1f;
        transform.localScale = Vector3.one * (radius / 5f) * distMult;

        Destroy(gameObject, duration);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        tickTimer += Time.deltaTime;

        if (tickTimer >= TickRate)
        {
            tickTimer = 0f;
            ExecuteVacuumPull();
        }
    }

    private void ExecuteVacuumPull()
    {
        // 1. 获取缩放后的真实半径
        float realRadius = CombatSandbox.GetDist(PullRadius);
        Vector3 center = transform.position;

        // 2. 【核心修改】：只针对敌方单位
        var targets = CombatDirector.ActiveEnemies;

        foreach (var t in targets)
        {
            if (t == null || t.CurrentHP <= 0) continue;

            float dist = Vector3.Distance(center, t.transform.position);

            // 3. 范围判定
            if (dist <= realRadius)
            {
                // 找到怪物的大脑和刚体
                EnemyBrain enemy = t.GetComponent<EnemyBrain>();
                Rigidbody2D rb = t.GetComponent<Rigidbody2D>();

                if (enemy != null && rb != null)
                {
                    // 👇【硬控核心 A】：清理原本的移动速度
                    // 彻底终结 AI 速度与引力加速度的博弈，消除鬼畜抖动
                    rb.velocity = Vector2.zero;

                    // 👇【硬控核心 B】：强制延长硬直状态
                    // 让怪物在大黑洞期间无法执行任何 AI 逻辑，实现真正的“硬锁定”
                    enemy.ApplyImpulse(Vector2.zero, 0.1f);

                    // 4. 计算拉取
                    if (dist > 0.3f) // 防止重叠时的坐标抖动
                    {
                        Vector2 pullDir = (center - t.transform.position).normalized;

                        // 越靠近中心，拉力越强（模拟黑洞引力梯度）
                        float forceFalloff = 1f - (dist / realRadius);
                        float finalForce = PullForce * (1f + forceFalloff * 2f);

                        // 施加持续向心力
                        rb.AddForce(pullDir * finalForce, ForceMode2D.Impulse);
                    }
                }
            }
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0f, 1f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, CombatSandbox.GetDist(PullRadius));
    }
}