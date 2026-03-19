using UnityEngine;

[RequireComponent(typeof(DamageReceiver))]
public class TestEnemy : MonoBehaviour
{
    [Header("=== 敌人基础属性 ===")]
    public float Speed = 2f;
    public float MeleeDamage = 15f;
    public float AttackInterval = 1.5f;
    public float AttackRange = 1.5f; // 攻击距离提取出来

    private DamageReceiver myReceiver;
    private DamageReceiver currentTarget; // 当前锁定的目标
    private float attackTimer;
    private Rigidbody2D rb;

  private void Start()
    {
        myReceiver = GetComponent<DamageReceiver>();
        myReceiver.isEnemy = true;
        myReceiver.Initialize(100f, 50f);

        // 👇【自动长出肉体】：防止你忘了挂组件
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) { rb = gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.freezeRotation = true; }
        if (GetComponent<Collider2D>() == null) gameObject.AddComponent<BoxCollider2D>();
    }

    private void Update()
    {
        if (myReceiver.CurrentHP <= 0) return;

        // 1. 实时索敌：每帧（或用计时器每隔0.5秒）寻找最近的玩家单位
        FindNearestTarget();

        // 2. 如果场上一个玩家单位都没了，原地发呆或继续往左走（拆家）
        if (currentTarget == null)
        {
            transform.Translate(Vector3.left * Speed * Time.deltaTime);
            return;
        }

        float dist = Vector3.Distance(transform.position, currentTarget.transform.position);
        if (dist > AttackRange)
        {
            float realSpeed = Speed;
            if (CombatSandbox.Instance != null) realSpeed *= CombatSandbox.Instance.SpeedMultiplier;

            // 👇【核心物理】：彻底删除 MoveTowards，换成真实的刚体推力！
            Vector2 dir = (currentTarget.transform.position - transform.position).normalized;
            rb.velocity = dir * realSpeed;
        }
        else
        {
            // 👇【紧急手刹】：敌人到达攻击距离后，也必须把速度清零，否则惯性会推走机甲！
            rb.velocity = Vector2.zero;

            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                attackTimer = AttackInterval;
                currentTarget.TakeDamage(MeleeDamage, gameObject.name);
                Debug.Log($"【敌人猛击】{gameObject.name} 狠狠地捶了机甲一拳！造成 {MeleeDamage} 伤害！");
            }
        }
    }

    // 👇 全新的智能索敌：寻找最近的非敌人单位！
    private void FindNearestTarget()
    {
        var allReceivers = FindObjectsOfType<DamageReceiver>();
        float minDistance = float.MaxValue;
        DamageReceiver nearest = null;

        foreach (var r in allReceivers)
        {
            // 必须是活着的，且是友军（玩家单位）
            if (!r.isEnemy && r.CurrentHP > 0)
            {
                float d = Vector3.Distance(transform.position, r.transform.position);
                if (d < minDistance)
                {
                    minDistance = d;
                    nearest = r;
                }
            }
        }
        currentTarget = nearest;
    }
}