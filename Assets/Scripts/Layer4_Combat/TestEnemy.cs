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

      

        // 👇【层级剥离】：根节点只做物理推挤 (Enemy_Body)
        gameObject.layer = LayerMask.NameToLayer("Enemy_Body");

        rb = GetComponent<Rigidbody2D>();
        if (rb == null) { rb = gameObject.AddComponent<Rigidbody2D>(); rb.gravityScale = 0; rb.freezeRotation = true; }

        BoxCollider2D physicsCol = gameObject.AddComponent<BoxCollider2D>();
        physicsCol.isTrigger = false;

        // 👇【极其关键的分离】：把贴图和 Hitbox 挪到一个专门的子节点上！
        GameObject visualObj = new GameObject("Enemy_Visual_Hitbox");
        visualObj.transform.SetParent(this.transform, false);
        visualObj.layer = LayerMask.NameToLayer("Enemy_Hitbox"); // 专属受击层！

        // 把原来挂在自己身上的 SpriteRenderer 搬到子节点去（防呆，如果原来有的话）
        SpriteRenderer mySr = GetComponent<SpriteRenderer>();
        SpriteRenderer visSr = visualObj.AddComponent<SpriteRenderer>();
        if (mySr != null)
        {
            visSr.sprite = mySr.sprite;
            visSr.color = mySr.color;
            Destroy(mySr); // 删掉原有的，免得画两遍
        }

        myReceiver.Initialize(100f, 50f, visSr);
        // 在子节点上挂载触发器 (接子弹用)
        BoxCollider2D hitboxCol = visualObj.AddComponent<BoxCollider2D>();
        hitboxCol.isTrigger = true;

        if (visSr.sprite != null)
        {
            Vector2 spriteSize = visSr.sprite.bounds.size;
            physicsCol.size = new Vector2(spriteSize.x * 0.8f, spriteSize.y * 0.3f);
            physicsCol.offset = new Vector2(0f, -(spriteSize.y / 2f) + (physicsCol.size.y / 2f));

            DynamicDepthSorter sorter = gameObject.GetComponent<DynamicDepthSorter>();
            if (sorter == null) sorter = gameObject.AddComponent<DynamicDepthSorter>();
            sorter.YOffset = -(spriteSize.y / 2f);
        }
    }

    private void Update()
    {
        if (myReceiver.CurrentHP <= 0) return;

        // 👇【核心静默控制】：如果战斗导演存在，且发令枪还没响，全员原地罚站！
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsCombatActive)
        {
            if (rb != null) rb.velocity = Vector2.zero; // 物理手刹死死拉住
            return;
        }

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