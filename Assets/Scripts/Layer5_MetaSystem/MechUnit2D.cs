using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems; // 【新增】：用来侦测鼠标是否悬停在 UI 上！

// ==========================================
// 战场级机甲通用载体 (Battlefield Assembler)
// ==========================================
public class MechUnit2D : MonoBehaviour
{
    // 🌍 【灵魂绑定】：记住自己的灵魂数据
    private SavedUnitProfile bindedData;

    [Header("=== 核心引用 ===")]
    // 👇 【新增】：为了让机甲所有的零件都能同步移动，我们要有一个专门挂载所有图片的容器
    public Transform VisualRoot;

    [Header("=== 2D 排序层控制 ===")]
    [Tooltip("机甲整体所在的排序层名称")]
    public string SortingLayerName = "Default";
    [Tooltip("底盘的排序号 (后面的零件会在此基础上递增)")]
    public int BaseSortingOrder = 0;

    [Header("=== 2D PPU 像素转换比例 ===")]
    [Tooltip("通常默认是100像素=1个Unity单位。这里要和你在车间里设置的一致才能对齐！")]
    public float PixelsPerUnit = 100f; // 【神级对齐细节】

    // 👇【新增】：实战部署时的全局放大/缩水率！
    [Header("=== 战场视觉与物理缩放 ===")]
    [Range(0.1f, 5f)]
    public float GlobalBattleScale = 1.0f;

    // TODO: 这里未来会挂载 2D 移动 AI、武器控制、HP 逻辑脚本

    private void Awake()
    {
        // 如果你没有在 Inspector 里指定容器，咱们就自己建一个
        if (VisualRoot == null)
        {
            GameObject visualRootObj = new GameObject("UnitVisualContainer_2D");
            visualRootObj.transform.SetParent(this.transform, false);
            VisualRoot = visualRootObj.transform;
        }
    }

    // ==========================================
    // 核心接口：天降正义时的灵魂注射与【战场级实时拼装】！
    // ==========================================
    // ==========================================
    // 核心接口：天降正义时的灵魂注射与【战场级实时拼装】！
    // ==========================================
    public void InitUnitData(SavedUnitProfile data)
    {
        this.bindedData = data;
        this.name = $"[UNIT] {data.UnitName}";

        // 1. 清理旧的拼装图样 (防止重部署时重叠)
        foreach (Transform child in VisualRoot)
        {
            Destroy(child.gameObject);
        }
        transform.localScale = Vector3.one * GlobalBattleScale;
        // ==========================================================
        // 👇👇👇 【神级复刻】：2D 世界拼装算法 (已注入物理肉体与图层遗传)
        // ==========================================================

        // --- 1. 给总控根节点 (Root) 注入刚体灵魂 ---
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;          // 俯视角，无重力
        rb.freezeRotation = true;      // 锁定旋转，防止被撞成陀螺
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // --- 2. 生成底盘基座 (受击物理肉体) ---
        GameObject chassisObj = new GameObject("Visual_ChassisBase");
        chassisObj.transform.SetParent(VisualRoot, false);

        // 【基因遗传】：自动继承预制体最外层的 Layer (比如 PlayerMech)
        chassisObj.layer = this.gameObject.layer;

        SpriteRenderer chassisSR = chassisObj.AddComponent<SpriteRenderer>();
        chassisSR.sprite = data.ChassisData.ChassisSprite;
        chassisSR.sortingLayerName = SortingLayerName;
        chassisSR.sortingOrder = BaseSortingOrder;

        // 👇【核心物理】：给底盘加上碰撞体！SpriteRenderer 会自动帮它完美贴合图片大小！
        chassisObj.AddComponent<BoxCollider2D>();

        // --- 3. 按照插槽档案，把零件一个个“焊”上去 ---
        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            int slotIdx = data.SlotIndices[i];
            string compID = data.EquippedComponentIDs[i];
            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;
            var slotDef = data.ChassisData.Sockets[slotIdx];

            // B. 插槽基座
            GameObject slotObj = new GameObject($"Socket_{slotDef.SlotName}");
            slotObj.layer = this.gameObject.layer; // 【基因遗传】
            slotObj.transform.SetParent(chassisObj.transform, false);
            slotObj.transform.localPosition = slotDef.LocalPosition;
            slotObj.transform.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            // C. Hinge 转轴
            GameObject hingeObj = new GameObject("Component_Hinge");
            hingeObj.layer = this.gameObject.layer; // 【基因遗传】
            hingeObj.transform.SetParent(slotObj.transform, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            // D. Visual 图片
            GameObject visObj = new GameObject("Visual_VisualSprite");
            visObj.layer = this.gameObject.layer; // 【基因遗传】
            visObj.transform.SetParent(hingeObj.transform, false);
            SpriteRenderer compSR = visObj.AddComponent<SpriteRenderer>();
            compSR.sprite = comp.BaseData.ComponentIcon;
            compSR.sortingLayerName = SortingLayerName;
            compSR.sortingOrder = BaseSortingOrder + 1;
            visObj.transform.localPosition = -comp.BaseData.AnchorOffset;
        }
        Debug.Log($"【天降正义】[{data.UnitName}] 已成功降落战场！当前 HP: {data.CurrentHP}");
        // 1. 翻译数据：把 UI 里的 SavedUnitProfile 翻译成战斗能用的 RuntimeChimeraData
        RuntimeChimeraData combatData = new RuntimeChimeraData();

        // 我们需要把装备的 ID 转换回真实的 SO 图纸数组
        List<ComponentDataSO> compBlueprints = new List<ComponentDataSO>();
        foreach (string compID in data.EquippedComponentIDs)
        {
            var compInstance = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (compInstance != null && compInstance.BaseData != null)
            {
                compBlueprints.Add(compInstance.BaseData);
            }
        }

        // 呼叫组装函数，生成战斗面板！
        combatData.Assemble(data.ChassisData, compBlueprints.ToArray());

        // 2. 激活肉体受击感知 (DamageReceiver)
        DamageReceiver receiver = GetComponent<DamageReceiver>();
        if (receiver == null) receiver = gameObject.AddComponent<DamageReceiver>();
        receiver.isEnemy = false; // 降落的肯定是友军！
        receiver.Initialize(combatData.MaxHP, combatData.MaxAP);

        // 3. 激活大脑 AI (ChimeraAIController)
        ChimeraAIController aiController = GetComponent<ChimeraAIController>();
        if (aiController == null) aiController = gameObject.AddComponent<ChimeraAIController>();
        aiController.Initialize(combatData);
        // 4. 激活所有武器模块 (WeaponModule)
        // 【究极修复】：按图索骥，精准找到底盘下的插槽，接通武器神经！
        int weaponDataIndex = 0;
        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            string compID = data.EquippedComponentIDs[i];
            var compInstance = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);

            // 确认这个插槽上装的真的是武器！
            if (compInstance != null && compInstance.BaseData.Type == ComponentType.Weapon)
            {
                var slotDef = data.ChassisData.Sockets[data.SlotIndices[i]];

                // 在 VisualRoot 的所有子孙节点里，无论藏多深，直接硬搜这个插槽！
                Transform[] allChildren = VisualRoot.GetComponentsInChildren<Transform>(true);
                Transform socketTrans = null;
                foreach (var child in allChildren)
                {
                    if (child.name == $"Socket_{slotDef.SlotName}")
                    {
                        socketTrans = child;
                        break;
                    }
                }

                if (socketTrans != null)
                {
                    WeaponModule weaponScript = socketTrans.gameObject.AddComponent<WeaponModule>();
                    weaponScript.Initialize(combatData.EquippedWeapons[weaponDataIndex]);
                    weaponDataIndex++;
                    Debug.Log($"【武装完毕】武器 [{compInstance.BaseData.ComponentName}] 成功挂载到了插槽 [{slotDef.SlotName}] 上！");
                }
            }
        }
    }

    private bool isDragging = false;
    private Vector3 dragStartPos;
    private Rigidbody2D rb;
    private Collider2D col;

    private void Start()
    {
        // 缓存物理组件，方便在拖拽时开关
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    // 1. 玩家鼠标按下机甲的瞬间
    private void OnMouseDown()
    {
        // TODO: 未来在这里加一个判断，如果是“战斗中(Combat Phase)”，就 return 不允许拖拽！
        // 目前咱们先假设全天候允许拖拽。

        isDragging = true;
        dragStartPos = transform.position; // 记住被抓起来前的位置

        // 变成半透明，并暂时关闭物理推挤，防止被拖着走的时候撞飞队友！
        TintMech(new Color(1f, 1f, 1f, 0.5f));
        if (rb != null) rb.isKinematic = true;
        if (col != null) col.enabled = false; // 暂时关闭自己的碰撞体，防止干扰放手时的雷达扫描！
    }

    // 2. 玩家按住鼠标拖拽的过程中
    private void OnMouseDrag()
    {
        if (!isDragging) return;

        // 让机甲跟着鼠标走
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0; // 锁死 Z 轴
        transform.position = mousePos;
    }

    // 3. 玩家松开鼠标的瞬间 (审判时刻)
    private void OnMouseUp()
    {
        if (!isDragging) return;
        isDragging = false;

        // 恢复视觉状态
        TintMech(Color.white);
        if (rb != null) rb.isKinematic = false;

        // --- 审判分支 A：玩家把它扔进了下方的 UI 机库里？(退役回收) ---
        if (EventSystem.current.IsPointerOverGameObject())
        {
            if (col != null) col.enabled = true; // 回收前恢复碰撞体
            RecycleToHangar();
            return;
        }

        // --- 审判分支 B：玩家想把它换个位置，扔在了地砖上？ ---
        // 👇【核心修复】：用 OverlapPointAll 穿透扫描！防止被自己或其他东西挡住！
        Collider2D[] hits = Physics2D.OverlapPointAll(transform.position);
        bool isValidZone = false;

        foreach (var hit in hits)
        {
            // 只要探针穿透的这一堆东西里，有一块地砖是 DeployZone，就判定成功！
            if (hit.CompareTag("DeployZone"))
            {
                isValidZone = true;
                break;
            }
        }

        if (isValidZone)
        {
            // 换位成功！稳稳落地！
            Debug.Log($"【阵型调整】[{bindedData.UnitName}] 重新部署到了新坐标: {transform.position}");
        }
        else
        {
            // 扔到了虚空或者墙上？直接瞬移回抓起前的位置 (防错容错机制)
            Debug.LogWarning("【部署失败】目标区域无效，机甲退回原位！");
            transform.position = dragStartPos;
        }

        // 👇【核心时机修复】：扫描完脚底板之后，再把自己的物理碰撞体打开，防止自己绊倒自己！
        if (col != null) col.enabled = true;
    }

    // 4. 核心回收逻辑：分解肉体，灵魂归位！
    private void RecycleToHangar()
    {
        Debug.Log($"【回收成功】[{bindedData.UnitName}] 已退出现役，返回机库！");

        // 1. 大管家数据重置
        if (bindedData != null)
        {
            bindedData.IsDeployed = false;
            // TODO: 未来在这里执行【返还电力负荷】逻辑！
        }

        // 2. 呼叫机库 UI，让刚才变灰的格子重新亮起来！
        if (HangarMenuUI.Instance != null)
        {
            HangarMenuUI.Instance.RefreshHangar();
        }

        // 3. 销毁这具场上的物理肉体
        Destroy(gameObject);
    }

    // 附带一个染色小工具，复用咱们 AI 里的变色逻辑
    private void TintMech(Color targetColor)
    {
        SpriteRenderer[] allRenderers = GetComponentsInChildren<SpriteRenderer>();
        foreach (var sr in allRenderers) sr.color = targetColor;
    }
}