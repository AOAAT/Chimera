using System.Collections.Generic;
using UnityEngine;

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

        // ==========================================================
        // 👇👇👇 【神级复刻】：2D 世界拼装算法 (已修复挤压 Bug)
        // ==========================================================

        // 2. 生成底盘基座 (chassisObj)
        GameObject chassisObj = new GameObject("Visual_ChassisBase");
        chassisObj.transform.SetParent(VisualRoot, false);
        SpriteRenderer chassisSR = chassisObj.AddComponent<SpriteRenderer>();
        chassisSR.sprite = data.ChassisData.ChassisSprite;

        // 设置底盘的排序层和层级，强制排在所有零件后面
        chassisSR.sortingLayerName = SortingLayerName;
        chassisSR.sortingOrder = BaseSortingOrder;

        // 3. 按照插槽档案，把零件一个个“焊”上去
        for (int i = 0; i < data.SlotIndices.Count; i++)
        {
            int slotIdx = data.SlotIndices[i];
            string compID = data.EquippedComponentIDs[i];

            var comp = PlayerInventoryManager.Instance.ComponentInventory.Find(c => c.InstanceID == compID);
            if (comp == null || comp.BaseData == null) continue;
            var slotDef = data.ChassisData.Sockets[slotIdx];

            // B. 插槽基座
            GameObject slotObj = new GameObject($"Socket_{slotDef.SlotName}");
            slotObj.transform.SetParent(chassisObj.transform, false);
            // 👇 【核心正骨修复 1】：去掉除法！直接使用世界坐标！
            slotObj.transform.localPosition = slotDef.LocalPosition;
            slotObj.transform.localRotation = Quaternion.Euler(0, 0, slotDef.MountAngle);

            // C. Hinge 转轴
            GameObject hingeObj = new GameObject("Component_Hinge");
            hingeObj.transform.SetParent(slotObj.transform, false);
            hingeObj.transform.localRotation = Quaternion.Euler(0, 0, comp.BaseData.BaseRotationOffset);
            hingeObj.transform.localScale = Vector3.one * (slotDef.DefaultComponentScale * comp.BaseData.VisualScaleMultiplier);

            // D. Visual 图片
            GameObject visObj = new GameObject("Visual_VisualSprite");
            visObj.transform.SetParent(hingeObj.transform, false);
            SpriteRenderer compSR = visObj.AddComponent<SpriteRenderer>();
            compSR.sprite = comp.BaseData.ComponentIcon;
            compSR.sortingLayerName = SortingLayerName;
            compSR.sortingOrder = BaseSortingOrder + 1;

            // 👇 【核心正骨修复 2】：去掉除法！直接使用世界坐标偏移！
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
}