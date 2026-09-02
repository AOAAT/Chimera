// --- START OF FILE DamageReceiver.cs ---
using System;
using UnityEngine;
using UnityEngine.Serialization;

public class DamageReceiver : MonoBehaviour
{
    [FormerlySerializedAs("isEnemy")]
    [SerializeField] private bool enemyFaction;

    // 保留原有调用名称，但每次阵营变化都会同步全局单位登记表。
    public bool isEnemy
    {
        get => enemyFaction;
        set
        {
            if (enemyFaction != value)
            {
                UnregisterUnit();
                enemyFaction = value;
            }

            if (isActiveAndEnabled) RegisterUnit();
        }
    }
    public float MaxHP { get; private set; }
    public float MaxAP { get; private set; }
    public float BaseBlock { get; private set; }

    public event System.Action OnEntityDeath;
    [Header("实时状态")]
    public float CurrentHP;
    public float CurrentAP;

    // 👇 UI 监听事件
    public event Action OnStatsChanged;

    public void Initialize(float maxHP, float maxAP, SpriteRenderer sr = null, float baseBlock = 0f)
    {
        MaxHP = maxHP; MaxAP = maxAP;
        BaseBlock = Mathf.Max(0f, baseBlock);
        CurrentHP = maxHP; CurrentAP = maxAP;
        OnStatsChanged?.Invoke();
    }

    // 👇 加入 isCrit 参数
    public void TakeDamage(float rawDamage, string sourceName, bool isTrueDamage = false, bool isCrit = false)
    {
        if (CurrentHP <= 0) return;

        // 基础格挡在单位初始化时由机甲装配数据或敌人图纸明确注入。
        float myBlock = BaseBlock;

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
        // 先从错误阵营移除，再登记到当前阵营；执行多次也不会产生重复项。
        if (enemyFaction)
        {
            CombatDirector.ActivePlayerUnits.Remove(this);
            if (!CombatDirector.ActiveEnemies.Contains(this)) CombatDirector.ActiveEnemies.Add(this);
        }
        else
        {
            CombatDirector.ActiveEnemies.Remove(this);
            if (!CombatDirector.ActivePlayerUnits.Contains(this)) CombatDirector.ActivePlayerUnits.Add(this);
        }
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

        CurrentHP = 0;

        // 通知所有子弹：这个目标已经不在扫描清单里了
        this.enabled = false;

        OnEntityDeath?.Invoke();
    }

    private void UnregisterUnit()
    {
        // 同时清理两张表，能够修复旧版本已经留下的错误登记。
        CombatDirector.ActiveEnemies.Remove(this);
        CombatDirector.ActivePlayerUnits.Remove(this);
    }
}
