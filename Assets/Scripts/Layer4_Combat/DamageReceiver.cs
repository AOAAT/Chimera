using UnityEngine;

// 统一的受击判定组件
public class DamageReceiver : MonoBehaviour
{
    public bool isEnemy; // 用于区分阵营

    public float MaxHP { get; private set; }
    public float MaxAP { get; private set; }

    [Header("实时状态 (运行时查看)")]
    public float CurrentHP;
    public float CurrentAP;

    private SpriteRenderer rendererReference;

    public void Initialize(float maxHP, float maxAP, SpriteRenderer sr = null)
    {
        MaxHP = maxHP;
        MaxAP = maxAP;
        CurrentHP = maxHP;
        CurrentAP = maxAP;
        rendererReference = sr;
    }

    // 核心物理交互：接收伤害
    public void TakeDamage(float rawDamage, string sourceName)
    {
        if (CurrentHP <= 0) return; // 已经死了

        float finalDamage = rawDamage;
        string unitType = isEnemy ? "敌人" : "玩家";

        // 1. AP（护甲）优先承伤逻辑
        if (CurrentAP > 0)
        {
            if (finalDamage <= CurrentAP)
            {
                CurrentAP -= finalDamage;
                Debug.Log($"<color=#00FFFF>【护甲吸收】</color> [{unitType}] {gameObject.name} 被 {sourceName} 命中，护甲吸收了 {finalDamage} 点伤害！剩余 AP: {CurrentAP}");
                finalDamage = 0;
            }
            else
            {
                finalDamage -= CurrentAP;
                Debug.Log($"<color=#FF8800>【护甲击穿】</color> [{unitType}] {gameObject.name} 被 {sourceName} 击穿护甲！溢出真实伤害: {finalDamage}");
                CurrentAP = 0;
            }
        }

        // 2. 溢出伤害由 HP（血量）承担
        if (finalDamage > 0)
        {
            CurrentHP -= finalDamage;
            Debug.Log($"<color=#FF0000>【真实伤害】</color> [{unitType}] {gameObject.name} 受到 {finalDamage} 点真实伤害！剩余 HP: {CurrentHP}");

            // 闪红视觉反馈
            if (rendererReference != null)
            {
                rendererReference.color = Color.red;
                Invoke(nameof(ResetColor), 0.1f);
            }

            if (CurrentHP <= 0)
            {
                CurrentHP = 0;
                Debug.Log($"<color=#FF0000><b>【单位阵亡】</b></color> [{unitType}] {gameObject.name} 被 {sourceName} 击毁！");
            }
        }
    }

    private void ResetColor()
    {
        if (rendererReference != null) rendererReference.color = Color.white;
    }
}