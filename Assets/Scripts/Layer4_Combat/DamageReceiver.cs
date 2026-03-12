using UnityEngine;

// 统一的受击判定组件
public class DamageReceiver : MonoBehaviour
{
    public bool isEnemy; // 用于区分阵营

    [Header("实时状态 (运行时查看)")]
    public float CurrentHP;
    public float CurrentAP;

    // 初始化血量和护甲
    public void Initialize(float maxHP, float maxAP)
    {
        CurrentHP = maxHP;
        CurrentAP = maxAP;
    }

    // 核心物理交互：接收伤害
    public void TakeDamage(float rawDamage, string sourceName)
    {

       // Debug.LogWarning($"【抓内鬼】{gameObject.name} 受到伤害！来源:{sourceName}\n追踪路径:\n{StackTraceUtility.ExtractStackTrace()}");
        if (CurrentHP <= 0) return; // 已经死了

        float finalDamage = rawDamage;

        // 1. AP（护甲）优先承伤逻辑
        if (CurrentAP > 0)
        {
            if (finalDamage <= CurrentAP)
            {
                CurrentAP -= finalDamage;
                Debug.Log($"【命中】{sourceName} 攻击了 {gameObject.name}，护甲完全吸收了 {finalDamage} 点伤害！剩余 AP: {CurrentAP}");
                finalDamage = 0;
            }
            else
            {
                finalDamage -= CurrentAP;
                Debug.Log($"【击穿】{sourceName} 击穿了 {gameObject.name} 的护甲！护甲损耗: {CurrentAP}，溢出伤害: {finalDamage}");
                CurrentAP = 0;
            }
        }

        // 2. 溢出伤害由 HP（血量）承担
        if (finalDamage > 0)
        {
            CurrentHP -= finalDamage;
            Debug.Log($"【流血】{gameObject.name} 受到了 {finalDamage} 点真实肉体伤害！剩余 HP: {CurrentHP}");

            if (CurrentHP <= 0)
            {
                Die();
            }
        }
    }

    private void Die()
    {
        Debug.LogWarning($"【死亡】{gameObject.name} 已被摧毁！");
        // 测试阶段，直接变红代表死亡
        GetComponent<SpriteRenderer>().color = Color.red;
        this.enabled = false;
    }
}