using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;

public class MechUnit2D : MonoBehaviour
{
    private SavedUnitProfile bindedData;

    // 【核心持有】：存储该机甲在本次战斗中的所有运行时状态和全局连携积木
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
    private Collider2D physicsCol;
    private bool isDragging = false;
    private Vector3 dragStartPos;

    private void Awake()
    {
        if (VisualRoot == null)
        {
            GameObject visualRootObj = new GameObject("UnitVisualContainer_2D");
            visualRootObj.transform.SetParent(this.transform, false);
            VisualRoot = visualRootObj.transform;
        }

        // 确保挂载排序组，解决 2D 遮挡关系
        SortingGroup sg = GetComponent<SortingGroup>();
        if (sg == null)
        {
            sg = gameObject.AddComponent<SortingGroup>();
        }
        sg.sortingLayerName = SortingLayerName;
    }

    /// <summary>
    /// 初始化机甲：构建视觉、注入物理参数、启动逻辑黑盒
    /// </summary>
    public void InitUnitData(SavedUnitProfile data)
    {
        this.bindedData = data;
        this.name = $"[UNIT] {data.UnitName}";

        // 1. 视觉层级清理
        foreach (Transform child in VisualRoot)
        {
            Destroy(child.gameObject);
        }

        transform.position = new Vector3(transform.position.x, transform.position.y, -0.01f);
        transform.localScale = Vector3.one * GlobalBattleScale;
        gameObject.layer = LayerMask.NameToLayer("Player_Body");

        // 2. 初始化物理刚体
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody2D>();
        }
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 3. 注入脚底物理推挤碰撞盒 (不作为受击框)
        if (physicsCol == null)
        {
            physicsCol = gameObject.AddComponent<BoxCollider2D>();
        }
        ((BoxCollider2D)physicsCol).isTrigger = false;

        // 4. 构建底盘视觉实体
        GameObject chassisObj = new GameObject("Visual_ChassisBase");
        chassisObj.transform.SetParent(VisualRoot, false);
        chassisObj.layer = LayerMask.NameToLayer("Player_Hitbox");

        SpriteRenderer chassisSR = chassisObj.AddComponent<SpriteRenderer>();
        chassisSR.sprite = data.ChassisData.ChassisSprite;
        chassisSR.sortingLayerName = SortingLayerName;
        chassisSR.sortingOrder = BaseSortingOrder;

        // 5. 自动适配脚底碰撞板尺寸
        Vector2 spriteSize = chassisSR.sprite.bounds.size;
        ((BoxCollider2D)physicsCol).size = new Vector2(spriteSize.x * 0.7f, spriteSize.y * 0.25f);
        ((BoxCollider2D)physicsCol).offset = new Vector2(0f, -(spriteSize.y / 2f) + (physicsCol.bounds.extents.y));

        // 6. 添加全身接弹受击框 (触发器)
        BoxCollider2D hitboxCol = chassisObj.AddComponent<BoxCollider2D>();
        hitboxCol.isTrigger = true;
        hitboxCol.size = new Vector2(spriteSize.x * 0.9f, spriteSize.y * 0.9f);
        hitboxCol.offset = Vector2.zero;

        // 7. 注入动态深度排序引擎
        DynamicDepthSorter sorter = gameObject.GetComponent<DynamicDepthSorter>();
        if (sorter == null)
        {
            sorter = gameObject.AddComponent<DynamicDepthSorter>();
        }
        sorter.YOffset = -(spriteSize.y / 2f);

        // 8. 递归挂载组件贴图 (插槽逻辑)
        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            int slotIdx = data.SlotIndices[i];
            string compID = data.EquippedComponentIDs[i];
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;

            var slotDef = data.ChassisData.Sockets[slotIdx];

            // 创建 Socket 节点
            GameObject slotObj = new GameObject($"Socket_{slotDef.SlotName}");
            slotObj.layer = this.gameObject.layer;
            slotObj.transform.SetParent(chassisObj.transform, false);
            slotObj.transform.localPosition = slotDef.LocalPosition;
            slotObj.transform.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            // 创建转轴节点
            GameObject hingeObj = new GameObject("Component_Hinge");
            hingeObj.layer = this.gameObject.layer;
            hingeObj.transform.SetParent(slotObj.transform, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            // 创建贴图节点
            GameObject visObj = new GameObject("Visual_VisualSprite");
            visObj.layer = this.gameObject.layer;
            visObj.transform.SetParent(hingeObj.transform, false);
            SpriteRenderer compSR = visObj.AddComponent<SpriteRenderer>();
            compSR.sprite = comp.BaseData.ComponentIcon;
            compSR.sortingLayerName = SortingLayerName;
            compSR.sortingOrder = BaseSortingOrder + 1;

            // 应用图纸设定的锚点修正
            visObj.transform.localPosition = -comp.BaseData.AnchorOffset;
        }

        // 9. 启动逻辑系统
        ActivateCombatBrains(data);

        // 10. 初始化辅助功能
        if (GetComponent<BuffManager>() == null) gameObject.AddComponent<BuffManager>();

        ProceduralAnimator2D procAnim = GetComponent<ProceduralAnimator2D>() ?? gameObject.AddComponent<ProceduralAnimator2D>();
        procAnim.SetTargetVisual(chassisObj.transform);
        procAnim.RefreshBaseState();
    }

    private void ActivateCombatBrains(SavedUnitProfile data)
    {
        // 构建本局战斗的运行时数据黑盒
        cachedCombatData = new RuntimeChimeraData();
        cachedCombatData.UnitID = data.UnitID;

        // 聚合当前所有安装的组件实例
        InstancedComponent[] tempInstances = new InstancedComponent[data.ChassisData.Sockets.Count];
        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            int slotIdx = data.SlotIndices[i];
            string compID = data.EquippedComponentIDs[i];
            var compInstance = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (compInstance != null)
            {
                tempInstances[slotIdx] = compInstance;
            }
        }

        // --- 执行核心装配流程 (初始化全局连携池) ---
        cachedCombatData.Assemble(data.ChassisData, tempInstances);

        // 初始化受击反馈组件
        DamageReceiver receiver = GetComponent<DamageReceiver>() ?? gameObject.AddComponent<DamageReceiver>();
        receiver.isEnemy = false;
        receiver.Initialize(cachedCombatData.MaxHP, cachedCombatData.MaxAP);

        // 血量同步
        if (data.CurrentHP > 0)
        {
            receiver.CurrentHP = Mathf.Min(data.CurrentHP, cachedCombatData.MaxHP);
        }
        else
        {
            receiver.CurrentHP = cachedCombatData.MaxHP;
        }

        // 初始化 AI 控制器
        ChimeraAIController aiController = GetComponent<ChimeraAIController>() ?? gameObject.AddComponent<ChimeraAIController>();
        aiController.Initialize(cachedCombatData);

        // 初始化技能控制器
        MechSkillController skillCtrl = GetComponent<MechSkillController>() ?? gameObject.AddComponent<MechSkillController>();
        skillCtrl.Initialize(cachedCombatData);

        // 初始化各个武器插槽的火控脚本
        int weaponDataIndex = 0;
        for (int i = 0; i < tempInstances.Length; i++)
        {
            var compInstance = tempInstances[i];
            if (compInstance != null && compInstance.BaseData.Type == ComponentType.Weapon)
            {
                if (weaponDataIndex >= cachedCombatData.EquippedWeapons.Count) break;

                var slotDef = data.ChassisData.Sockets[i];
                Transform socketTrans = VisualRoot.FindRecursive($"Socket_{slotDef.SlotName}");

                if (socketTrans != null)
                {
                    WeaponModule weaponScript = socketTrans.gameObject.AddComponent<WeaponModule>();

                    // 将机甲黑盒 combatData 作为 Owner 传入武器，开启连携支持
                    weaponScript.Initialize(
                        cachedCombatData.EquippedWeapons[weaponDataIndex],
                        cachedCombatData,
                        cachedCombatData.LogicCenterOffset,
                        this.transform
                    );
                }
                weaponDataIndex++;
            }
        }
    }

    /// <summary>
    /// 指挥官正式下达开战指令：激活全机组件的“开战被动”积木
    /// </summary>
    public void ExecuteBattleStartProtocol()
    {
        if (this.cachedCombatData == null)
        {
            Debug.LogWarning($"<color=red>[警告]</color> 机甲 {gameObject.name} 尚未初始化黑盒数据，无法执行开战协议。");
            return;
        }

        Debug.Log($"<color=#00FFFF>【协议启动】</color> 机甲 [{cachedCombatData.UnitName}] 被动模组通电，执行 ECA 管线...");

        ECAContext startContext = new ECAContext
        {
            ImpactPoint = transform.position,
            PrimaryTarget = this.transform,
            SourceEntity = this.transform,
            ChassisData = this.cachedCombatData,
            IsEnemyFire = false
        };

        // 执行全机搜集的所有开战积木 (例如大象腿的定时冲撞 Buff 挂载)
        foreach (var action in this.cachedCombatData.GlobalOnBattleStartActions)
        {
            if (action != null)
            {
                Debug.Log($"<color=#00FFFF>-> 触发积木: {action.name}</color>");
                action.Execute(startContext);
            }
        }
    }

    // ==========================================
    // 战场部署拖拽逻辑
    // ==========================================

    private void OnMouseDown()
    {
        if (CombatDirector.Instance != null && !CombatDirector.Instance.IsDeploymentPhase) return;
        isDragging = true;
        dragStartPos = transform.position;
        TintMech(new Color(1f, 1f, 1f, 0.5f));
        if (rb != null) rb.isKinematic = true;
        if (physicsCol != null) physicsCol.enabled = false;
    }

    private void OnMouseDrag()
    {
        if (!isDragging) return;
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = new Vector3(mousePos.x, mousePos.y, -0.01f);
    }

    private void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;
        TintMech(Color.white);
        if (rb != null) rb.isKinematic = false;

        // 回收到机库检测
        if (EventSystem.current.IsPointerOverGameObject())
        {
            if (physicsCol != null) physicsCol.enabled = true;
            RecycleToHangar();
            return;
        }

        // 禁飞区与部署合法性判定
        int noDeployLayerMask = LayerMask.GetMask("NoDeploy");
        Collider2D forbiddenHit = Physics2D.OverlapCircle(transform.position, 0.5f, noDeployLayerMask);

        Collider2D[] hits = Physics2D.OverlapPointAll(transform.position);
        bool isValidZone = false;
        foreach (var hit in hits)
        {
            if (hit.CompareTag("DeployZone")) { isValidZone = true; break; }
        }

        if (forbiddenHit != null || !isValidZone)
        {
            transform.position = dragStartPos;
        }

        if (physicsCol != null) physicsCol.enabled = true;
    }

    private void RecycleToHangar()
    {
        if (bindedData != null) bindedData.IsDeployed = false;
        if (HangarMenuUI.Instance != null) HangarMenuUI.Instance.RefreshHangar();
        Destroy(gameObject);
    }

    private void TintMech(Color targetColor)
    {
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in allRenderers) sr.color = targetColor;
    }

    public void SyncPostCombatState()
    {
        if (bindedData == null) return;
        DamageReceiver receiver = GetComponent<DamageReceiver>();
        if (receiver != null)
        {
            bindedData.CurrentHP = Mathf.Max(0, receiver.CurrentHP);
            bindedData.CurrentAP = receiver.MaxAP;
        }
    }

    private void LateUpdate()
    {
        EnforceArenaBounds();
    }

    private void EnforceArenaBounds()
    {
        if (CombatDirector.Instance == null || CombatDirector.Instance.CurrentArenaSize.x == 0) return;
        Vector2 center = CombatDirector.Instance.CurrentArenaCenter;
        Vector2 size = CombatDirector.Instance.CurrentArenaSize;
        float minX = center.x - size.x / 2f, maxX = center.x + size.x / 2f;
        float minY = center.y - size.y / 2f, maxY = center.y + size.y / 2f;
        Vector3 cp = transform.position;
        float cx = Mathf.Clamp(cp.x, minX, maxX), cy = Mathf.Clamp(cp.y, minY, maxY);
        if (cp.x != cx || cp.y != cy)
        {
            transform.position = new Vector3(cx, cy, cp.z);
            if (rb != null) rb.velocity = Vector2.zero;
        }
    }
}

// ==========================================
// 辅助类：递归查找 Transform
// ==========================================
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