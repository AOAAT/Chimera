using UnityEngine;

[RequireComponent(typeof(DamageReceiver))]
public class TestEnemy : MonoBehaviour
{
    [Header("敌人内置数据")]
    public float Speed = 20f; // 相当于 EnginePower
    public float MeleeDamage = 15f;
    public float AttackInterval = 1.5f;

    private DamageReceiver myReceiver;
    private DamageReceiver playerCore;
    private float attackTimer;

    private void Start()
    {
        myReceiver = GetComponent<DamageReceiver>();
        myReceiver.isEnemy = true;
        myReceiver.Initialize(100f, 50f); // 给它 100血，50甲

        // 假设咱们的奇美拉挂载了 DamageReceiver 并且 isEnemy = false
        // 为了白盒测试，它直接去抱玩家大腿
        var all = FindObjectsOfType<DamageReceiver>();
        foreach (var a in all) if (!a.isEnemy) playerCore = a;
    }

    private void Update()
    {
        if (myReceiver.CurrentHP <= 0 || playerCore == null || playerCore.CurrentHP <= 0) return;

        float dist = Vector3.Distance(transform.position, playerCore.transform.position);

        // 假设它的近战攻击距离是 1.5 米
        if (dist > 1.5f)
        {
            // 移动：应用全局度量衡！
            float realSpeed = Speed * CombatSandbox.Instance.SpeedMultiplier;
            transform.position = Vector3.MoveTowards(transform.position, playerCore.transform.position, realSpeed * Time.deltaTime);
        }
        else
        {
            // 贴脸近战攻击
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0)
            {
                playerCore.TakeDamage(MeleeDamage, "测试生化兽");
                attackTimer = AttackInterval;
            }
        }
    }
}