// --- START OF FILE DamageReceiver.cs ---
using System;
using UnityEngine;

public class DamageReceiver : MonoBehaviour
{
    public bool isEnemy;
    public float MaxHP { get; private set; }
    public float MaxAP { get; private set; }

    public event System.Action OnEntityDeath;
    [Header("实时状态")]
    public float CurrentHP;
    public float CurrentAP;

    // 👇 UI 监听事件
    public event Action OnStatsChanged;

    public void Initialize(float maxHP, float maxAP, SpriteRenderer sr = null)
    {
        MaxHP = maxHP; MaxAP = maxAP;
        CurrentHP = maxHP; CurrentAP = maxAP;
        OnStatsChanged?.Invoke();
    }

    // 👇 加入 isCrit 参数
    public void TakeDamage(float rawDamage, string sourceName, bool isTrueDamage = false, bool isCrit = false)
    {
        if (CurrentHP <= 0) return;

        // 👇【核心新增】：去查自己的格挡值！
        float myBlock = 0f;
        if (!isEnemy)
        {
            // 玩家机甲：去查 RuntimeChimeraData 的黑盒
            var ai = GetComponent<ChimeraAIController>();
            // (你需要稍微改一下 ChimeraAIController 让他暴露出 runtimeData)
            // 这里为了极简，你可以直接存一个公开的 GetTotalBlock() 或者查 BuffMgr
        }
        else
        {
            // 怪物：查图纸
            var brain = GetComponent<EnemyBrain>();
            if (brain != null && brain.MyData != null) myBlock = brain.MyData.GetStat(StatType.Block);
        }

        // 动态叠加上 Buff 提供的临时格挡！(完美支持未来你出加格挡的技能)
        BuffManager myBuffs = GetComponent<BuffManager>();
        if (myBuffs != null && myBuffs.BuffStatModifiers.ContainsKey(StatType.AddedBlock))
        {
            myBlock += myBuffs.BuffStatModifiers[StatType.AddedBlock];
        }

        // 👇 把 myBlock 传进新公式
        GameFormulas.CalcDamageReduction(rawDamage, CurrentAP, myBlock, isTrueDamage, out float finalDamage, out float absorbed);

        CurrentAP -= absorbed;
        CurrentHP -= finalDamage;

        // 👇 呼叫飘字总线
        if (DamagePopupManager.Instance != null)
        {
            Vector3 popPos = transform.position + Vector3.up * 1.5f;

            if (absorbed > 0.1f)
                DamagePopupManager.Instance.SpawnPopup(popPos, absorbed, false, false, true, isEnemy == false);

            if (finalDamage > 0.1f)
                DamagePopupManager.Instance.SpawnPopup(popPos, finalDamage, isCrit, isTrueDamage, false, isEnemy == false);
        }

        // 👇 呼叫血条更新
        OnStatsChanged?.Invoke();

        if (CurrentHP <= 0) Die();
    }

    private void Die()
    {
        this.enabled = false;

        EntityHUD hud = GetComponentInChildren<EntityHUD>();
        if (hud != null) hud.gameObject.SetActive(false);

        // 呼叫大脑执行死亡剧本
        OnEntityDeath?.Invoke();
    }
}