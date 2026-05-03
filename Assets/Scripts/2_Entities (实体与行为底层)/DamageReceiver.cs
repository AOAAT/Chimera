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

        float myBlock = 0f;
        // 获取基础格挡 (来自底盘/零件/怪物图纸)
        if (!isEnemy) { /* 玩家逻辑：获取底盘基础格挡 */ }
        else
        {
            var brain = GetComponent<EnemyBrain>();
            if (brain != null && brain.MyData != null) myBlock = brain.MyData.GetStat(StatType.Block);
        }

        // --- 👇【关键修复】：格挡值现在也享受百分比加成了！ ---
        BuffManager myBuffs = GetComponent<BuffManager>();
        if (myBuffs != null)
        {
            myBlock = myBuffs.GetAdjustedStat(StatType.AddedBlock, myBlock);
        }
        // ---------------------------------------------------

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

    private void Start()
    {
        // 【优化】：出生即注册，避免全局搜索
        RegisterUnit();
    }

    private void RegisterUnit()
    {
        if (isEnemy) { if (!CombatDirector.ActiveEnemies.Contains(this)) CombatDirector.ActiveEnemies.Add(this); }
        else { if (!CombatDirector.ActivePlayerUnits.Contains(this)) CombatDirector.ActivePlayerUnits.Add(this); }
    }

    private void OnDestroy()
    {
        // 【优化】：彻底杜绝野指针
        UnregisterUnit();
    }


    // --- DamageReceiver.cs 的 Die 方法 ---

    private void Die()
    {
        UnregisterUnit();
        this.enabled = false;

        // 【新增】：禁用本物体及所有子物体上的碰撞盒
        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }

        EntityHUD hud = GetComponentInChildren<EntityHUD>();
        if (hud != null) hud.gameObject.SetActive(false);

        OnEntityDeath?.Invoke();
    }

    private void UnregisterUnit()
    {
        if (isEnemy) CombatDirector.ActiveEnemies.Remove(this);
        else CombatDirector.ActivePlayerUnits.Remove(this);
    }
}