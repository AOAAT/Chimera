using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;
using System.Linq;

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
    private bool isDragging = false;
    private Vector3 dragStartPos;

    private float eliteLingerTime = 2f;
    private float eliteFadeTime = 1.5f;

    private void Awake()
    {
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

        // 基础排序组初始化
        SortingGroup sg = GetComponent<SortingGroup>();
        if (sg == null) sg = gameObject.AddComponent<SortingGroup>();
        sg.sortingLayerName = SortingLayerName;
    }

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

    // ==========================================
    // 🛠️ 核心驱动：加固后的全量装配流程
    // ==========================================
    private void FullSetup(SavedUnitProfile data, InstancedComponent[] comps, bool isEnemy, EnemyDataSO enemySO = null)
    {
        // 1. 暴力初始化核心组件（解决所有 MissingComponentException）
        rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();

        physicsCol = GetComponent<BoxCollider2D>();
        if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();

        SortingGroup sg = GetComponent<SortingGroup>();
        if (sg == null) sg = gameObject.AddComponent<SortingGroup>();

        // 2. 基础属性与阵营层级
        this.bindedData = data;
        this.name = (isEnemy ? "[ELITE] " : "[UNIT] ") + data.UnitName;

        // 强制同步清理视觉残骸
        if (VisualRoot == null) Awake();
        List<GameObject> children = new List<GameObject>();
        foreach (Transform child in VisualRoot) children.Add(child.gameObject);
        children.ForEach(c => DestroyImmediate(c));

        transform.position = new Vector3(transform.position.x, transform.position.y, 0f);
        transform.localScale = Vector3.one * GlobalBattleScale;
        gameObject.layer = LayerMask.NameToLayer(isEnemy ? "Enemy_Body" : "Player_Body");

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        physicsCol.isTrigger = false;
        sg.sortingLayerName = SortingLayerName;

        // 3. 构建底盘视觉实体
        GameObject chassisObj = new GameObject("Visual_ChassisBase");
        chassisObj.transform.SetParent(VisualRoot, false);

        SpriteRenderer chassisSR = chassisObj.AddComponent<SpriteRenderer>();
        BoxCollider2D hitboxCol = chassisObj.AddComponent<BoxCollider2D>(); // 物理受击盒

        chassisSR.sprite = data.ChassisData.ChassisSprite;
        chassisSR.sortingLayerName = SortingLayerName;
        chassisSR.sortingOrder = BaseSortingOrder;

        hitboxCol.isTrigger = true;
        Vector2 spriteSize = chassisSR.sprite != null ? chassisSR.sprite.bounds.size : Vector2.one;
        hitboxCol.size = new Vector2(spriteSize.x * 0.9f, spriteSize.y * 0.9f);

        // 4. 适配根节点物理碰撞板 (脚底板)
        physicsCol.size = new Vector2(spriteSize.x * 0.7f, spriteSize.y * 0.25f);
        physicsCol.offset = new Vector2(0f, -(spriteSize.y / 2f) + (physicsCol.size.y / 2f));

        // 5. 阴影系统层级锁定
        UnitFactionShadow shadowComp = GetComponent<UnitFactionShadow>() ?? gameObject.AddComponent<UnitFactionShadow>();
        shadowComp.EnsureShadowObject();
        shadowComp.GetShadowTransform().SetParent(chassisObj.transform, false);
        shadowComp.GetShadowTransform().SetAsFirstSibling();

        // 6. 零件挂载循环
        for (int i = 0; i < comps.Length; i++)
        {
            if (comps[i] == null) continue;
            var comp = comps[i];
            var slotDef = data.ChassisData.Sockets[i];

            string typeTag = (comp.BaseData.Type == ComponentType.Movement) ? "_MovementType" : "";
            GameObject slotObj = new GameObject($"Socket_{slotDef.SlotName}{typeTag}");
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

        // 7. 逻辑初始化（注意：这里现在会返回 receiver 引用）
        DamageReceiver receiver = ActivateCombatBrainsSafe(data, comps, isEnemy, chassisObj.transform);

        // 8. 阴影数据应用
        if (isEnemy && enemySO != null && enemySO.OverrideShadow)
            shadowComp.SetupManualShadow(true, enemySO.ShadowWidth, enemySO.ShadowHeight, enemySO.ShadowOffset);
        else
            shadowComp.SetupModularShadow(isEnemy, spriteSize.x, -(spriteSize.y / 2f));

        // 9. 递归设置物理层级 (解决命中判定问题)
        SetLayerRecursive(chassisObj, isEnemy ? LayerMask.NameToLayer("Enemy_Hitbox") : LayerMask.NameToLayer("Player_Hitbox"));

        // 10. 深度排序与动画
        DynamicDepthSorter sorter = GetComponent<DynamicDepthSorter>() ?? gameObject.AddComponent<DynamicDepthSorter>();
        sorter.YOffset = -(spriteSize.y / 2f);

        ProceduralAnimator2D procAnim = GetComponent<ProceduralAnimator2D>() ?? gameObject.AddComponent<ProceduralAnimator2D>();
        procAnim.SetTargetVisual(chassisObj.transform);
        procAnim.RefreshBaseState();

        // 11. 【精英死亡剧本】：如果是敌人，订阅死亡事件
        if (isEnemy && receiver != null)
        {
            receiver.OnEntityDeath += HandleEliteDeath;
        }
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
        cachedCombatData.Assemble(data.ChassisData, comps);

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
    private void OnMouseDown() { if (GetComponent<DamageReceiver>().isEnemy) return; if (CombatDirector.Instance != null && !CombatDirector.Instance.IsDeploymentPhase) return; isDragging = true; dragStartPos = transform.position; TintMech(new Color(1f, 1f, 1f, 0.5f)); if (rb != null) rb.isKinematic = true; if (physicsCol != null) physicsCol.enabled = false; }
    private void OnMouseDrag() { if (!isDragging) return; Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition); transform.position = new Vector3(mousePos.x, mousePos.y, -0.01f); }
    private void OnMouseUp() { if (!isDragging) return; isDragging = false; TintMech(Color.white); if (rb != null) rb.isKinematic = false; if (EventSystem.current.IsPointerOverGameObject()) { RecycleToHangar(); return; } int noDeployLayerMask = LayerMask.GetMask("NoDeploy"); Collider2D f = Physics2D.OverlapCircle(transform.position, 0.5f, noDeployLayerMask); bool v = Physics2D.OverlapPointAll(transform.position).Any(h => h.CompareTag("DeployZone")); if (f != null || !v) transform.position = dragStartPos; if (physicsCol != null) physicsCol.enabled = true; }
    private void RecycleToHangar() { if (bindedData != null) bindedData.IsDeployed = false; if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.RefreshHangar(); Destroy(gameObject); }
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
    private void LateUpdate() { if (CombatDirector.Instance == null || CombatDirector.Instance.CurrentArenaSize.x == 0) return; Vector2 center = CombatDirector.Instance.CurrentArenaCenter; Vector2 size = CombatDirector.Instance.CurrentArenaSize; float minX = center.x - size.x / 2f, maxX = center.x + size.x / 2f, minY = center.y - size.y / 2f, maxY = center.y + size.y / 2f; Vector3 cp = transform.position; float cx = Mathf.Clamp(cp.x, minX, maxX), cy = Mathf.Clamp(cp.y, minY, maxY); if (cp.x != cx || cp.y != cy) { transform.position = new Vector3(cx, cy, cp.z); if (rb != null) rb.velocity = Vector2.zero; } }
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