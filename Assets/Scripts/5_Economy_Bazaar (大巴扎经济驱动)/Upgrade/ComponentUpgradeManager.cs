// --- START OF FILE ComponentUpgradeManager.cs ---
using System;
using System.Linq;
using UnityEngine;

public class ComponentUpgradeManager : MonoBehaviour
{
    public static ComponentUpgradeManager Instance;

    public event Action<InstancedComponent> OnComponentUpgraded;

    private void Awake() { if (Instance == null) Instance = this; }

    public bool TryInitiateUpgrade(InstancedComponent targetItem, out UpgradePreviewData previewData, out string errorMsg)
    {
        previewData = null;
        errorMsg = string.Empty;

        if (targetItem == null) return false;

        if (targetItem.CurrentLevel >= targetItem.BaseData.LevelMatrix.Count)
        {
            errorMsg = "该组件已达到最高等级，无法继续强化！";
            return false;
        }

        // 👇【体验升级】：目标物品（TargetItem）即使装在机甲上也可以发起强化！
        // 我们只要求祭品（MaterialItem）必须是闲置的！
        var materialItem = PlayerInventoryManager.Instance.ComponentInventory.FirstOrDefault(c =>
            c.InstanceID != targetItem.InstanceID &&
            c.BaseData.ComponentBaseID == targetItem.BaseData.ComponentBaseID &&
            c.CurrentLevel == targetItem.CurrentLevel &&
            !c.IsEquipped  // 【绝对安全锁】：祭品绝对不能是装在机甲上的！
        );

        if (materialItem == null)
        {
            errorMsg = "仓库中缺乏同级别且闲置的相同组件作为材料！";
            return false;
        }

        previewData = targetItem.GenerateUpgradePreview(materialItem);
        return true;
    }

    public void ConfirmAndExecuteUpgrade(UpgradePreviewData previewData)
    {
        if (previewData == null || previewData.TargetItem == null || previewData.MaterialItem == null) return;

        PlayerInventoryManager.Instance.ComponentInventory.Remove(previewData.MaterialItem);

        previewData.TargetItem.CurrentLevel++;

        Debug.Log($"<color=#00FF00>【系统提示】</color> 强化成功！[{previewData.TargetItem.BaseData.ComponentName}] 突破至 Lv.{previewData.TargetItem.CurrentLevel}！");

        OnComponentUpgraded?.Invoke(previewData.TargetItem);
        PlayerInventoryManager.Instance.ForceTriggerInventoryEvent();

        GlobalAudioManager.Instance.PlayUISound(UISoundType.UI_UpgradeSuccess);

        // 配合一点打击感：震下屏幕
        if (ScreenEffectManager.Instance != null)
            ScreenEffectManager.Instance.TriggerShake(0.2f, 0.2f);
    }
}