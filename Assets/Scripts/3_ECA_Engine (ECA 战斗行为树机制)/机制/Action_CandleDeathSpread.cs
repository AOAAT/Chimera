using UnityEngine;
using System.Linq;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "CandleDeathSpread", menuName = "Chimera Protocol/2. ECA 机制积木/特殊 - 烛火死后传染")]
public class Action_CandleDeathSpread : ECAAction
{
    [Header("=== 资源引用 ===")]
    [Tooltip("必须拖入‘烛火’对应的 Buff 资产，用于读取层数")]
    public BuffDataSO CandleBuff;

    [Tooltip("传染时喷射出的子弹预制体 (建议用蓝紫色幽灵火苗)")]
    public GameObject ProjectilePrefab;

    [Tooltip("必须拖入‘仪式蜡烛’组件的图纸，用于读取等级属性")]
    public ComponentDataSO CandleWeaponSO;

    [Header("=== 传染参数 ===")]
    [Tooltip("层数与弹丸数量的转化比。如 0.5 代表 10 层烛火产生 5 枚弹丸")]
    [Range(0.1f, 2.0f)] public float TransferRatio = 0.5f;

    [Tooltip("每枚弹丸造成的固定伤害")]
    public float DamagePerBullet = 10f;

    public override void Execute(ECAContext context)
    {
        // 1. 基础合法性检查
        if (context.SourceEntity == null || ProjectilePrefab == null || CandleBuff == null || CandleWeaponSO == null)
        {
            Debug.LogWarning("【烛火传染中断】由于缺少 SourceEntity、预制体或图纸引用，操作无法执行。");
            return;
        }

        // 2. 获取死者身上的 Buff 状态
        BuffManager deadBuffMgr = context.SourceEntity.GetComponent<BuffManager>();
        if (deadBuffMgr == null) return;

        int stacks = deadBuffMgr.GetBuffStacks(CandleBuff.BuffID);
        if (stacks <= 0) return;

        // 3. 【核心进阶】：动态回溯机甲黑盒，获取武器的真实实时等级
        int currentWeaponLevel = 1; // 默认保底 1 级
        if (context.ChassisData != null)
        {
            // 在机甲已装备的武器列表中，通过图纸比对找到那把“蜡烛”
            var runtimeWeapon = context.ChassisData.EquippedWeapons.FirstOrDefault(w => w.SourceSO == CandleWeaponSO);
            if (runtimeWeapon != null)
            {
                currentWeaponLevel = runtimeWeapon.CurrentLevel;
            }
        }

        // 4. 寻找下个受害者 (死者的盟友，即怪物的同伙)
        DamageReceiver myReceiver = context.SourceEntity.GetComponent<DamageReceiver>();
        bool sourceIsEnemySide = (myReceiver != null && myReceiver.isEnemy);

        // 获取当前阵营的所有活着的成员 (不含死者自己)
        var allies = sourceIsEnemySide ? CombatDirector.ActiveEnemies : CombatDirector.ActivePlayerUnits;
        var nextVictim = allies
            .Where(e => e != null && e.CurrentHP > 0 && e.transform != context.SourceEntity)
            .OrderBy(e => Vector3.Distance(context.SourceEntity.position, e.transform.position))
            .FirstOrDefault();

        if (nextVictim == null)
        {
            Debug.Log("<color=yellow>【烛火消散】</color> 视野内没有可传染的活体目标。");
            return;
        }

        // 5. 构造一个包含“等级数据”的虚拟武器，用于初始化子弹
        RuntimeWeapon dummyWeapon = new RuntimeWeapon
        {
            WeaponName = "遗志烛火",
            SourceSO = CandleWeaponSO,
            CurrentLevel = currentWeaponLevel
        };

        // 从图纸中提取对应等级的数值矩阵 (解决弹速为 0 的关键)
        var levelData = CandleWeaponSO.GetLevelData(currentWeaponLevel);
        if (levelData != null)
        {
            // 拷贝属性字典
            foreach (var entry in levelData.Stats)
            {
                dummyWeapon.WeaponStats[entry.StatID] = entry.Value;
            }
            // 拷贝命中积木 (这是实现“无限传染”逻辑链的关键)
            if (levelData.OnHitActions != null)
            {
                dummyWeapon.OnHitActions.AddRange(levelData.OnHitActions);
            }
        }

        // 6. 暴力喷射传染弹
        int bulletCount = Mathf.Max(1, Mathf.FloorToInt(stacks * TransferRatio));
        Debug.Log($"<color=#FF4500>【烛火传染】</color> 目标带着 {stacks} 层死于 Lv.{currentWeaponLevel} 蜡烛，发射 {bulletCount} 枚火苗！");

        for (int i = 0; i < bulletCount; i++)
        {
            // 给予一个初始随机旋转，让火苗炸开感更强
            Quaternion randomRot = Quaternion.Euler(0, 0, Random.Range(0, 360f));

            // 从对象池抓取子弹
            GameObject projObj = SimplePool.Spawn(ProjectilePrefab, context.SourceEntity.position, randomRot);
            Projectile pScript = projObj.GetComponent<Projectile>();

            if (pScript != null)
            {
                // 参数对齐最新 9 参数接口：
                // 目标, 伤害, 武器数据(已带等级属性), 黑盒, 自身Transform, 是否敌火, 是否暴击, 代际(0), 是否打队友(false)
                pScript.Fire(
                    nextVictim.transform,
                    DamagePerBullet,
                    dummyWeapon,
                    context.ChassisData,
                    context.SourceEntity,
                    sourceIsEnemySide,
                    false,
                    0,
                    false
                );
            }
        }
    }
}