using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(SortingGroup))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(BoxCollider2D))]
public class MechUnit2D : MonoBehaviour
{
    private SavedUnitProfile bindedData;
    private RuntimeChimeraData cachedCombatData;

    [Header("=== 核心引用 ===")]
    public Transform VisualRoot;

    [Header("=== 2D 排序层控制 ===")]
    public string SortingLayerName = "Entities";
    public int BaseSortingOrder = 0;

    [Header("=== 战场视觉与物理缩放 ===")]
    [Range(0.1f, 5f)]
    public float GlobalBattleScale = 1.0f;

    private Rigidbody2D rb;
    private BoxCollider2D physicsCol; // 根节点物理推挤盒


    private float eliteLingerTime = 2f;
    private float eliteFadeTime = 1.5f;

    private void Awake()
    {
        // 🌟 [重构 Awake]：采用“先拿后补”策略，彻底解决 MissingComponent 异常
        SortingGroup sg = GetComponent<SortingGroup>();
        if (sg == null) sg = gameObject.AddComponent<SortingGroup>();
        sg.sortingLayerName = SortingLayerName;

        if (VisualRoot == null)
        {
            Transform found = transform.Find("UnitVisualContainer_2D");
            if (found != null) VisualRoot = found;
            else
            {
                GameObject visualRootObj = new GameObject("UnitVisualContainer_2D");
                visualRootObj.transform.SetParent(this.transform, false);
                VisualRoot = visualRootObj.transform;
            }
        }
    }
    public SavedUnitProfile GetProfile() => bindedData;
    private void Update()
    {
        if (CombatDirector.Instance == null || !CombatDirector.Instance.IsCombatActive) return;

        var receiver = GetComponent<DamageReceiver>();
        if (cachedCombatData == null || (receiver != null && receiver.CurrentHP <= 0)) return;

        foreach (var kvp in cachedCombatData.ComponentToRuntimeMap)
        {
            ComponentDataSO compSO = kvp.Key;
            RuntimeWeapon runtimeProxy = kvp.Value;

            if (runtimeProxy.OnTickActions == null || runtimeProxy.OnTickActions.Count == 0) continue;

            ECAContext tickContext = new ECAContext
            {
                SourceEntity = this.transform,
                ChassisData = this.cachedCombatData,
                SourceComponentSO = compSO,
                SourceWeapon = runtimeProxy,
                ImpactPoint = this.transform.position,
                IsEnemyFire = receiver.isEnemy,
                CustomStates = this.cachedCombatData.PersistentStates
            };

            foreach (var action in runtimeProxy.OnTickActions)
            {
                if (action == null || tickContext.ExecutionAborted) break;
                action.Execute(tickContext);
            }
        }
    }

  
    private int compInstance_Level_Placeholder(ComponentDataSO so) => 1;
    // ==========================================
    // 🚀 入口 A：玩家机甲初始化
    // ==========================================
    public void InitUnitData(SavedUnitProfile data)
    {
        int totalSockets = data.ChassisData.Sockets.Count;
        InstancedComponent[] tempInstances = new InstancedComponent[totalSockets];
        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            int slotIdx = data.SlotIndices[i];
            var compInstance = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == data.EquippedComponentIDs[i]);
            if (compInstance != null && slotIdx < totalSockets) tempInstances[slotIdx] = compInstance;
        }
        FullSetup(data, tempInstances, false);
    }

    // ==========================================
    // 🚀 入口 B：精英敌人初始化
    // ==========================================
    public void InitAsEliteEnemy(EnemyDataSO enemySO)
    {
        SavedUnitProfile eliteProfile = new SavedUnitProfile(new InstancedChassis(enemySO.Chassis), enemySO.EnemyName);
        int totalSockets = enemySO.Chassis.Sockets.Count;
        InstancedComponent[] comps = new InstancedComponent[totalSockets];
        for (int i = 0; i < enemySO.Components.Count; i++)
        {
            if (i >= totalSockets || enemySO.Components[i] == null) continue;
            comps[i] = new InstancedComponent(enemySO.Components[i], enemySO.EliteComponentLevel);
            eliteProfile.SlotIndices.Add(i);
            eliteProfile.EquippedComponentIDs.Add("ELITE_TEMP_" + i);
        }
        this.eliteLingerTime = enemySO.CorpseLingerTime; // 👈 注入停留时长
        this.eliteFadeTime = enemySO.FadeDuration;       // 👈 注入渐变时长
        FullSetup(eliteProfile, comps, true, enemySO);
    }

    public void ReAssemble()
    {
        InitUnitData(bindedData);
        Debug.Log($"<color=cyan>【系统】</color> 机甲 {bindedData.UnitName} 已完成实时改装刷新。");
    }

    public void RecycleToWarehouse()
    {
        if (bindedData == null) return;

        Debug.Log($"<color=red>【回收协议】</color> 正在拆解机甲: {bindedData.UnitName}");

        // 1. 归还底盘实物
        PlayerInventoryManager.Instance.AddChassisToWarehouse(bindedData.ChassisData, 1);

        // 2. 遍历并归还所有挂载的零件实物
        foreach (var instanceID in bindedData.EquippedComponentIDs)
        {
            // 从全局实例库中找回这个零件的型号数据
            var compInstance = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == instanceID);
            if (compInstance != null)
            {
                PlayerInventoryManager.Instance.AddComponentToWarehouse(compInstance.BaseData, compInstance.CurrentMark, 1);
            }
        }

        // 3. 视觉反馈与销毁
        if (GlobalAudioManager.Instance != null) GlobalAudioManager.Instance.PlayUISound(UISoundType.Mech_Detach);
        // 这里未来可以产生一个烟雾特效
        Destroy(gameObject);
    }

    private void FullSetup(SavedUnitProfile data, InstancedComponent[] comps, bool isEnemy, EnemyDataSO enemySO = null)
    {
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        physicsCol = GetComponent<BoxCollider2D>();
        if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();
        // 🌟 注入滑溜溜材质
        PhysicsMaterial2D slippery = Resources.Load<PhysicsMaterial2D>("Slippery_Material");
        if (slippery != null) rb.sharedMaterial = slippery;

        SortingGroup sg = GetComponent<SortingGroup>() ?? gameObject.AddComponent<SortingGroup>();

        this.bindedData = data;
        this.name = (isEnemy ? "[ELITE] " : "[UNIT] ") + data.UnitName;

        if (VisualRoot == null) Awake();
        foreach (Transform child in VisualRoot) DestroyImmediate(child.gameObject);

        transform.localScale = Vector3.one * GlobalBattleScale;
        gameObject.layer = LayerMask.NameToLayer(isEnemy ? "Enemy_Body" : "Player_Body");

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        physicsCol.isTrigger = false;
        sg.sortingLayerName = SortingLayerName;

        GameObject chassisObj = new GameObject("Visual_ChassisBase");
        chassisObj.transform.SetParent(VisualRoot, false);
        SpriteRenderer chassisSR = chassisObj.AddComponent<SpriteRenderer>();
        chassisSR.sprite = data.ChassisData.ChassisSprite;
        chassisSR.sortingLayerName = SortingLayerName;

        BoxCollider2D hitboxCol = chassisObj.AddComponent<BoxCollider2D>();
        hitboxCol.isTrigger = true;
        Vector2 spriteSize = chassisSR.sprite != null ? chassisSR.sprite.bounds.size : Vector2.one;
        hitboxCol.size = spriteSize * 0.9f;

        physicsCol.size = new Vector2(spriteSize.x * 0.7f, spriteSize.y * 0.25f);
        physicsCol.offset = new Vector2(0f, -(spriteSize.y / 2f) + (physicsCol.size.y / 2f));

        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) continue;
            var comp = comps[i];
            var slotDef = data.ChassisData.Sockets[i];

            GameObject slotObj = new GameObject($"Socket_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);
            slotObj.transform.localPosition = slotDef.LocalPosition;
            slotObj.transform.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            GameObject hingeObj = new GameObject("Component_Hinge");
            hingeObj.transform.SetParent(slotObj.transform, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            GameObject visObj = new GameObject("Visual_VisualSprite");
            visObj.transform.SetParent(hingeObj.transform, false);
            SpriteRenderer cpSR = visObj.AddComponent<SpriteRenderer>();
            cpSR.sprite = comp.BaseData.ComponentIcon;
            cpSR.sortingLayerName = SortingLayerName;
            cpSR.sortingOrder = BaseSortingOrder + 1;
            visObj.transform.localPosition = -comp.BaseData.AnchorOffset;
        }

        ActivateCombatBrainsSafe(data, comps, isEnemy, chassisObj.transform);
        SetLayerRecursive(chassisObj, isEnemy ? LayerMask.NameToLayer("Enemy_Hitbox") : LayerMask.NameToLayer("Player_Body"));

        DynamicDepthSorter sorter = GetComponent<DynamicDepthSorter>() ?? gameObject.AddComponent<DynamicDepthSorter>();
        sorter.YOffset = -(spriteSize.y / 2f);
    }
    private void SetLayerRecursive(GameObject obj, int newLayer)
    {
        // 如果是影子，强制保持在 Default 层，防止干扰子弹
        if (obj.name == "Logic_Visual_Shadow")
        {
            obj.layer = LayerMask.NameToLayer("Default");
        }
        else
        {
            obj.layer = newLayer;
        }

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursive(child.gameObject, newLayer);
        }
    }

    private DamageReceiver ActivateCombatBrainsSafe(SavedUnitProfile data, InstancedComponent[] comps, bool isEnemy, Transform chassisRoot)
    {
        cachedCombatData = new RuntimeChimeraData();
        // 关键点：我们需要在 Assemble 之后，让 Actions 能够找到当前的 GameObject
        cachedCombatData.Assemble(data.ChassisData, comps, this.transform); // 传入 this.transform


        DamageReceiver receiver = GetComponent<DamageReceiver>() ?? gameObject.AddComponent<DamageReceiver>();
        receiver.isEnemy = isEnemy;
        receiver.Initialize(cachedCombatData.MaxHP, cachedCombatData.MaxAP);
        receiver.CurrentHP = (!isEnemy && data.CurrentHP > 0) ? Mathf.Min(data.CurrentHP, cachedCombatData.MaxHP) : cachedCombatData.MaxHP;

        // 大脑初始化
        var ai = GetComponent<ChimeraAIController>() ?? gameObject.AddComponent<ChimeraAIController>();
        ai.Initialize(cachedCombatData);
        var sc = GetComponent<MechSkillController>() ?? gameObject.AddComponent<MechSkillController>();
        sc.Initialize(cachedCombatData);

        // 武器火控初始化
        int weaponIndex = 0;
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] != null && comps[i].BaseData.Type == ComponentType.Weapon)
            {
                var slotDef = data.ChassisData.Sockets[i];
                Transform socketTrans = chassisRoot.FindRecursive($"Socket_{slotDef.SlotName}");
                if (socketTrans != null)
                {
                    WeaponModule w = socketTrans.gameObject.GetComponent<WeaponModule>() ?? socketTrans.gameObject.AddComponent<WeaponModule>();
                    w.Initialize(cachedCombatData.EquippedWeapons[weaponIndex], cachedCombatData, cachedCombatData.LogicCenterOffset, this.transform);
                }
                weaponIndex++;
            }
        }


        if (isEnemy)
        {
            receiver.OnEntityDeath += HandleEliteDeath;
        }
        else
        {
            receiver.OnEntityDeath += HandlePlayerDeath; // 👈 处理玩家自己
        }

        return receiver;

    }

    private void HandlePlayerDeath()
    {
        Debug.Log($"<color=red>【战损警告】</color> 机甲 [{this.name}] 核心熔毁，逻辑离线。");
        gameObject.layer = LayerMask.NameToLayer("Floor");
        SetLayerRecursive(VisualRoot.gameObject, LayerMask.NameToLayer("Floor"));

        // 2. 禁用所有受击框和推挤框
        foreach (var col in GetComponentsInChildren<Collider2D>())
        {
            col.enabled = false;
        }

        // 3. 停止物理模拟
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
        }

        // 1. 关停 AI 指令
        var ai = GetComponent<ChimeraAIController>();
        if (ai != null)
        {
            ai.AbortDash();
            ai.ClearMoveCommand();
            ai.enabled = false;
        }

        // 2. 关停技能控制
        var sc = GetComponent<MechSkillController>();
        if (sc != null) sc.enabled = false;

        // 3. 关停所有挂载的武器模块
        foreach (var w in GetComponentsInChildren<WeaponModule>())
        {
            w.enabled = false;
        }

        // 4. 物理停摆：防止机甲死后还在推挤别人
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
            rb.simulated = false; // 彻底从物理世界“消失”
        }

        // 5. 视觉处理：变暗
        TintMech(new Color(0.3f, 0.3f, 0.3f, 0.8f));

        // 6. 剥离受击层，防止子弹继续撞在残骸上
        gameObject.layer = LayerMask.NameToLayer("Floor");
        foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;
    }

    // --- 修改 MechUnit2D.cs 中的 HandleEliteDeath 方法 ---
    // --- 修改 MechUnit2D.cs 中的 HandleEliteDeath 方法 ---
    private void HandleEliteDeath()
    {
        Debug.Log($"<color=red>【精英战损】</color> [{this.name}] 已离线。执行清理程序...");

        // 1. 👇【核心修复】：在逻辑关停前，触发所有 Buff 的死亡管线（如：烛火传染）
        BuffManager bm = GetComponent<BuffManager>();
        DamageReceiver dr = GetComponent<DamageReceiver>();
        if (bm != null)
        {
            ECAContext deathContext = new ECAContext
            {
                ImpactPoint = transform.position,
                PrimaryTarget = this.transform,
                SourceEntity = this.transform, // 👈 极其关键：告诉积木火苗从哪喷出来
                IsEnemyFire = (dr != null) ? dr.isEnemy : true, // 按照精英怪的真实阵营判定
                ChassisData = this.cachedCombatData // 传入底盘数据，方便积木追溯武器等级
            };

            // 呼叫总线，让烛火、自爆等 Buff 效果爆发
            bm.TriggerHolderDeathActions(deathContext);
        }

        // 2. 彻底关停逻辑核心
        var ai = GetComponent<ChimeraAIController>();
        if (ai != null) ai.enabled = false;

        var eb = GetComponent<EnemyBrain>();
        if (eb != null) eb.enabled = false;

        var sc = GetComponent<MechSkillController>();
        if (sc != null) sc.enabled = false;

        // 3. 禁用所有挂载的武器模块
        foreach (var w in GetComponentsInChildren<WeaponModule>())
        {
            w.enabled = false;
        }

        // 4. 物理停摆
        if (rb != null)
        {
            rb.velocity = Vector2.zero;
            rb.isKinematic = true;
            rb.simulated = false;
        }

        // 5. 剥离受击能力
        gameObject.layer = LayerMask.NameToLayer("Floor");
        foreach (var col in GetComponentsInChildren<Collider2D>()) col.enabled = false;

        // 6. 视觉淡出
        StartCoroutine(EliteCorpseDecayRoutine());
    }

    private System.Collections.IEnumerator EliteCorpseDecayRoutine()
    {
        // 1. 使用动态注入的停留时长
        yield return new WaitForSeconds(this.eliteLingerTime);

        float elapsed = 0f;
        SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>();

        while (elapsed < this.eliteFadeTime) // 👈 使用动态注入的渐变时长
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / this.eliteFadeTime);
            foreach (var sr in srs)
            {
                if (sr.gameObject.name == "Logic_Visual_Shadow") continue;
                Color c = sr.color;
                c.a = alpha;
                sr.color = c;
            }
            yield return null;
        }
        Destroy(gameObject);
    }

    public void ExecuteBattleStartProtocol()
    {
        if (this.cachedCombatData == null) return;

        // 【核心修复】：动态获取当前实体的阵营，确保 ECA 积木知道是谁在开战
        bool amIEnemy = false;
        DamageReceiver dr = GetComponent<DamageReceiver>();
        if (dr != null) amIEnemy = dr.isEnemy;

        Debug.Log($"<color=#00FFFF>【协议启动】</color> [{(amIEnemy ? "精英" : "玩家")}] [{cachedCombatData.UnitName}] 被动模组通电...");

        ECAContext startContext = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = this.transform,
            SourceEntity = this.transform,
            ChassisData = this.cachedCombatData,
            IsEnemyFire = amIEnemy // 👈 关键：将真实的阵营标记传给积木
        };

        foreach (var action in this.cachedCombatData.GlobalOnBattleStartActions)
        {
            if (action != null) action.Execute(startContext);
        }
    }

    private void TintMech(Color targetColor) { SpriteRenderer[] srs = GetComponentsInChildren<SpriteRenderer>(); foreach (var sr in srs) if (sr.gameObject.name != "Logic_Visual_Shadow") sr.color = targetColor; }
    public void SyncPostCombatState()
    {
        if (bindedData == null) return;
        DamageReceiver r = GetComponent<DamageReceiver>();

        if (r != null)
        {
            // --- 方案 A：记忆性系统 (已注释，保留备用) ---
            // bindedData.CurrentHP = Mathf.Max(0, r.CurrentHP);
            // bindedData.CurrentAP = r.MaxAP;

            // --- 方案 B：V1.0 满血复活协议 ---
            // 直接从缓存的战斗数据中读取最大值，让机甲离场即满血
            bindedData.CurrentHP = cachedCombatData.MaxHP;
            bindedData.CurrentAP = cachedCombatData.MaxAP;

            Debug.Log($"<color=green>【自动修护】</color> 机甲 [{bindedData.UnitName}] 已完成战后整备，耐久度已恢复至 100%。");
        }
    }

}

public static class TransformExtensions
{
    public static Transform FindRecursive(this Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform result = FindRecursive(child, name);
            if (result != null) return result;
        }
        return null;
    }
}