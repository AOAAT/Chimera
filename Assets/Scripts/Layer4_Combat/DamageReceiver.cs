using UnityEngine;

public class DamageReceiver : MonoBehaviour
{
    public bool isEnemy;

    public float MaxHP { get; private set; }
    public float MaxAP { get; private set; }

    [Header("实时状态 (运行时查看)")]
    public float CurrentHP;
    public float CurrentAP;

    public void Initialize(float maxHP, float maxAP, SpriteRenderer sr = null)
    {
        MaxHP = maxHP;
        MaxAP = maxAP;
        CurrentHP = maxHP;
        CurrentAP = maxAP;
    }

    public void TakeDamage(float rawDamage, string sourceName, bool isTrueDamage = false)
    {
        if (CurrentHP <= 0) return; // 已经死了，拒收伤害

        float oldHP = CurrentHP;
        float oldAP = CurrentAP;
        float finalDamage = rawDamage;
        float absorbed = 0f;

        // 1. AP（护甲）承伤逻辑 —— 如果是真实伤害，直接跳过这一步！
        if (CurrentAP > 0 && !isTrueDamage)
        {
            if (finalDamage <= CurrentAP)
            {
                absorbed = finalDamage;
                CurrentAP -= finalDamage;
                finalDamage = 0;
            }
            else
            {
                absorbed = CurrentAP;
                finalDamage -= CurrentAP;
                CurrentAP = 0;
            }
        }

        // 2. 溢出伤害（或真实伤害）由 HP（血量）承担
        if (finalDamage > 0)
        {
            CurrentHP -= finalDamage;
        }

        // 3. 日志与视觉反馈
        string camp = isEnemy ? "敌人" : "玩家";
        string color = isEnemy ? "#FF4500" : "#00FFFF";

        // 如果是真伤，打印紫色的专属特效提示！
        string dmgTypeStr = isTrueDamage ? "<color=#FF00FF>[真实伤害 穿透了护甲！]</color>" : "";

        Debug.Log($"<color={color}><b>【伤害结算】</b></color> [{camp}] {gameObject.name} 被 <color=yellow>{sourceName}</color> 命中！{dmgTypeStr}\n" +
                  $"<color=#AAAAAA>▶ 承受总伤: {rawDamage:F1} (护甲吸收: {absorbed:F1}, 肉体受创: {finalDamage:F1})</color>\n" +
                  $"<color=#00FF00>▶ AP 变化: {oldAP:F1} -> {CurrentAP:F1}</color>\n" +
                  $"<color=#FF6666>▶ HP 变化: {oldHP:F1} -> {CurrentHP:F1}</color>");

        if (CurrentHP <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        string camp = isEnemy ? "敌人" : "玩家";
        Debug.Log($"<b><color=#8B0000>【单位阵亡】</color></b> [{camp}] {gameObject.name} 已被彻底摧毁！");

        // 死亡时尸体变黑，视觉反馈更直接
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in srs) sr.color = new Color(0.2f, 0.2f, 0.2f, 1f);

        this.enabled = false;
    }
}