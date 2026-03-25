using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering; // 【新增】：为了操作 SortingGroup
using UnityEngine.EventSystems;

// ==========================================
// 战场级机甲通用载体 (Battlefield Assembler)
// ==========================================
public class MechUnit2D : MonoBehaviour
{
    private SavedUnitProfile bindedData;

    [Header("=== 核心引用 ===")]
    public Transform VisualRoot;

    [Header("=== 2D 排序层控制 (方案一强制修正) ===")]
    [Tooltip("机甲整体所在的排序层名称，强制建议设为 Entities")]
    public string SortingLayerName = "Entities";
    [Tooltip("底盘的排序号")]
    public int BaseSortingOrder = 0;

    [Header("=== 战场视觉与物理缩放 ===")]
    [Range(0.1f, 5f)]
    public float GlobalBattleScale = 1.0f;

    private Rigidbody2D rb;
    private Collider2D physicsCol; // 脚底板物理碰撞体
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

        // --- 方案一：核心强制补丁 ---
        // 自动挂载 SortingGroup，并强制锁定层级为 Entities
        SortingGroup sg = GetComponent<SortingGroup>();
        if (sg == null) sg = gameObject.AddComponent<SortingGroup>();
        sg.sortingLayerName = SortingLayerName; //
    }

    public void InitUnitData(SavedUnitProfile data)
    {
        this.bindedData = data;
        this.name = $"[UNIT] {data.UnitName}";

        foreach (Transform child in VisualRoot) Destroy(child.gameObject);

        // 1. 物理坐标修正：Z轴稍微向前推一点点 (-0.01)，确保在3D空间也绝对靠前
        transform.position = new Vector3(transform.position.x, transform.position.y, -0.01f);
        transform.localScale = Vector3.one * GlobalBattleScale;

        // --- 2. 根节点注入：刚体与【脚底板软碰撞】 ---
        gameObject.layer = LayerMask.NameToLayer("Player_Body"); //

        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 【物理底座】：只占脚底30%，负责走位推搡 (非 Trigger)
        if (physicsCol == null) physicsCol = gameObject.AddComponent<BoxCollider2D>();
        ((BoxCollider2D)physicsCol).isTrigger = false;

        // --- 3. 生成底盘基座与【全身受击判定】 ---
        GameObject chassisObj = new GameObject("Visual_ChassisBase");
        chassisObj.transform.SetParent(VisualRoot, false);
        chassisObj.layer = LayerMask.NameToLayer("Player_Hitbox"); //

        SpriteRenderer chassisSR = chassisObj.AddComponent<SpriteRenderer>();
        chassisSR.sprite = data.ChassisData.ChassisSprite;
        chassisSR.sortingLayerName = SortingLayerName; // 强制图层遗传
        chassisSR.sortingOrder = BaseSortingOrder;

        // 动态计算尺寸：脚底板窄一点矮一点，受击盒大一点覆盖全身
        Vector2 spriteSize = chassisSR.sprite.bounds.size;
        ((BoxCollider2D)physicsCol).size = new Vector2(spriteSize.x * 0.7f, spriteSize.y * 0.25f);
        ((BoxCollider2D)physicsCol).offset = new Vector2(0f, -(spriteSize.y / 2f) + (physicsCol.bounds.extents.y));

        // 【受击判定盒】：全身覆盖 (必须是 Trigger)
        BoxCollider2D hitboxCol = chassisObj.AddComponent<BoxCollider2D>();
        hitboxCol.isTrigger = true;
        hitboxCol.size = new Vector2(spriteSize.x * 0.9f, spriteSize.y * 0.9f);
        hitboxCol.offset = Vector2.zero;

        // --- 4. 注入：动态深度排序引擎 ---
        DynamicDepthSorter sorter = gameObject.GetComponent<DynamicDepthSorter>();
        if (sorter == null) sorter = gameObject.AddComponent<DynamicDepthSorter>();
        sorter.YOffset = -(spriteSize.y / 2f); // 以脚底板为基准线排序

        // --- 5. 拼装零件 (遗传图层逻辑) ---
        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            int slotIdx = data.SlotIndices[i];
            string compID = data.EquippedComponentIDs[i];
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;
            var slotDef = data.ChassisData.Sockets[slotIdx];

            GameObject slotObj = new GameObject($"Socket_{slotDef.SlotName}");
            slotObj.layer = this.gameObject.layer;
            slotObj.transform.SetParent(chassisObj.transform, false);
            slotObj.transform.localPosition = slotDef.LocalPosition;
            slotObj.transform.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            GameObject hingeObj = new GameObject("Component_Hinge");
            hingeObj.layer = this.gameObject.layer;
            hingeObj.transform.SetParent(slotObj.transform, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            GameObject visObj = new GameObject("Visual_VisualSprite");
            visObj.layer = this.gameObject.layer;
            visObj.transform.SetParent(hingeObj.transform, false);
            SpriteRenderer compSR = visObj.AddComponent<SpriteRenderer>();
            compSR.sprite = comp.BaseData.ComponentIcon;
            compSR.sortingLayerName = SortingLayerName; // 强制图层遗传
            compSR.sortingOrder = BaseSortingOrder + 1;
            visObj.transform.localPosition = -comp.BaseData.AnchorOffset;
        }

        // --- 6. 战斗大脑激活逻辑 ---
        ActivateCombatBrains(data);
    }

    private void ActivateCombatBrains(SavedUnitProfile data)
    {
        RuntimeChimeraData combatData = new RuntimeChimeraData();
        List<ComponentDataSO> compBlueprints = new List<ComponentDataSO>();
        foreach (string compID in data.EquippedComponentIDs)
        {
            var compInstance = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (compInstance != null) compBlueprints.Add(compInstance.BaseData);
        }

        combatData.Assemble(data.ChassisData, compBlueprints.ToArray());

        DamageReceiver receiver = GetComponent<DamageReceiver>() ?? gameObject.AddComponent<DamageReceiver>();
        receiver.isEnemy = false;
        receiver.Initialize(combatData.MaxHP, combatData.MaxAP);

        ChimeraAIController aiController = GetComponent<ChimeraAIController>() ?? gameObject.AddComponent<ChimeraAIController>();
        aiController.Initialize(combatData);

        // 激活武器系统
        int weaponDataIndex = 0;
        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            var compInstance = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == data.EquippedComponentIDs[i]);
            if (compInstance != null && compInstance.BaseData.Type == ComponentType.Weapon)
            {
                var slotDef = data.ChassisData.Sockets[data.SlotIndices[i]];
                Transform socketTrans = VisualRoot.FindRecursive($"Socket_{slotDef.SlotName}"); // 辅助扩展方法

                if (socketTrans != null)
                {
                    WeaponModule weaponScript = socketTrans.gameObject.AddComponent<WeaponModule>();
                    weaponScript.Initialize(combatData.EquippedWeapons[weaponDataIndex], combatData.LogicCenterOffset, this.transform);
                    weaponDataIndex++;
                }
            }
        }
    }

    // --- 拖拽逻辑保持优化 ---
    private void OnMouseDown()
    {
        isDragging = true;
        dragStartPos = transform.position;
        TintMech(new Color(1f, 1f, 1f, 0.5f));
        if (rb != null) rb.isKinematic = true;
        if (physicsCol != null) physicsCol.enabled = false; // 拖拽时暂时取消物理体积，防止穿墙/撞队友
    }

    private void OnMouseDrag()
    {
        if (!isDragging) return;
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        transform.position = new Vector3(mousePos.x, mousePos.y, -0.01f); // 锁定Z轴
    }

    private void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;
        TintMech(Color.white);
        if (rb != null) rb.isKinematic = false;

        // 如果鼠标松开时指在 UI 上 (比如机库界面)，就把机甲回收
        if (EventSystem.current.IsPointerOverGameObject())
        {
            if (physicsCol != null) physicsCol.enabled = true;
            RecycleToHangar();
            return;
        }

        // ==========================================
        // 🚨 安检门 1：检测体积是否触碰到“禁飞区 (楚河汉界)”！
        // ==========================================
        int noDeployLayerMask = LayerMask.GetMask("NoDeploy");

        // 我们用一个 0.5f 半径的虚拟气泡进行扫描 (与 UI 拖拽时保持绝对一致)
        Collider2D forbiddenHit = Physics2D.OverlapCircle(transform.position, 0.5f, noDeployLayerMask);

        if (forbiddenHit != null)
        {
            Debug.LogWarning("【战术违规】指挥官！禁止将机甲转移至敌人区域！已强制退回原位！");

            // 核心惩罚逻辑：瞬间把机甲弹回这次拖拽前的初始位置！
            transform.position = dragStartPos;

            // 别忘了恢复物理碰撞
            if (physicsCol != null) physicsCol.enabled = true;
            return;
        }

        // ==========================================
        // ✅ 安检门 2：检测脚下是否有合法的 DeployZone (绿区地板)
        // ==========================================
        Collider2D[] hits = Physics2D.OverlapPointAll(transform.position);
        bool isValidZone = false;
        foreach (var hit in hits)
        {
            if (hit.CompareTag("DeployZone"))
            {
                isValidZone = true;
                break;
            }
        }

        if (isValidZone)
        {
            Debug.Log($"重新部署成功: {transform.position}");
        }
        else
        {
            Debug.LogWarning("【部署失败】目标坐标未铺设绿区地板，强制退回！");
            transform.position = dragStartPos; // 如果没绿区地板，也弹回原位
        }

        if (physicsCol != null) physicsCol.enabled = true; // 落地后恢复物理推挤
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
        // 如果没有绑定机库档案，说明这是个临时生成的怪物或测试机甲，不处理
        if (bindedData == null) return;

        DamageReceiver receiver = GetComponent<DamageReceiver>();
        if (receiver != null)
        {
            // 1. 真实战损保留：无论剩多少血，都原原本本地写回档案！
            // 使用 Mathf.Max(0, ...) 确保死透的机甲血量是 0 而不是负数
            bindedData.CurrentHP = Mathf.Max(0, receiver.CurrentHP);

            // 2. 护甲自动充能：把刚出厂时的最大护甲，重新充满！
            bindedData.CurrentAP = receiver.MaxAP;

            Debug.Log($"【数据同步】机甲 [{bindedData.UnitName}] 战损已回传机库！当前 HP: {bindedData.CurrentHP}, 护甲已重置为: {bindedData.CurrentAP}");
        }
    }
}

// 辅助扩展类：用于深层查找插槽
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
