using System;
using System.Linq;
using UnityEngine;

public class ComponentUpgradeManager : MonoBehaviour
{
    public static ComponentUpgradeManager Instance;

    // 全局广播：当组件成功升级时触发 (UI 和 组装车间监听此事件刷新面板)
    public event Action<InstancedComponent> OnComponentUpgraded;

    private void Awake() { if (Instance == null) Instance = this; }

    // ==========================================
    // 阶段 2：库存检索与拦截 (UI 点击 [强化] 时调用)
    // ==========================================
    public bool TryInitiateUpgrade(InstancedComponent targetItem, out UpgradePreviewData previewData, out string errorMsg)
    {
        previewData = null;
        errorMsg = string.Empty;

        if (targetItem == null) return false;

        // 拦截 1：等级上限检测
        if (targetItem.CurrentLevel >= targetItem.BaseData.LevelMatrix.Count)
        {
            errorMsg = "该组件已达到最高等级，无法继续强化！";
            return false;
        }

        // 拦截 2：寻找祭品 (必须同源、同级、且不能是自己、且最好是未装备的闲置品)
        var materialItem = PlayerInventoryManager.Instance.ComponentInventory.FirstOrDefault(c =>
            c.InstanceID != targetItem.InstanceID &&                       // 不是自己
            c.BaseData.ComponentBaseID == targetItem.BaseData.ComponentBaseID && // 同源
            c.CurrentLevel == targetItem.CurrentLevel &&                   // 同级
            !c.IsEquipped                                                  // 【安全锁】祭品必须在仓库里吃灰
        );

        if (materialItem == null)
        {
            errorMsg = "仓库中缺乏同级别且闲置的相同组件！";
            return false;
        }

        // 检索成功，生成只读的 Diff 预览数据给 UI
        previewData = targetItem.GenerateUpgradePreview(materialItem);
        return true;
    }

    // ==========================================
    // 阶段 3：确认强化 (UI 弹窗点击 [确认] 时调用)
    // ==========================================
    public void ConfirmAndExecuteUpgrade(UpgradePreviewData previewData)
    {
        if (previewData == null || previewData.TargetItem == null || previewData.MaterialItem == null)
        {
            Debug.LogError("【强化异常】预览数据丢失，终止操作！");
            return;
        }

        // 1. 彻底销毁祭品
        PlayerInventoryManager.Instance.ComponentInventory.Remove(previewData.MaterialItem);
        // (注：由于 C# 垃圾回收机制，从 List 移除后该实例即宣告死亡)

        // 2. 主体突变进化！(我们不需要重写数据，只需要升级 Level 即可，底层的 RuntimeChimeraData 会自动去读新矩阵！)
        previewData.TargetItem.CurrentLevel++;

        Debug.Log($"<color=#00FF00>【系统提示】</color> 强化成功！[{previewData.TargetItem.BaseData.ComponentName}] 突破至 Lv.{previewData.TargetItem.CurrentLevel}！");

        // 3. 触发全局大事件 (通知仓库更新格子，通知机库重新计算机甲属性)
        OnComponentUpgraded?.Invoke(previewData.TargetItem);
        PlayerInventoryManager.Instance.ForceTriggerInventoryEvent(); // 强制刷新全局仓库 UI
    }
}