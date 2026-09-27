using UnityEngine;

public static class AccessoryValidator
{
    /// <summary>
    /// 核心审计：该芯片是否能塞进这个零件？
    /// </summary>
    public static bool CanFitAccessory(InstancedComponent targetComp, AccessoryDataSO chipSO, out string failReason)
    {
        failReason = string.Empty;

        // --- 0. 插槽余量检查 ---
        if (targetComp.SocketedAccessoryIDs.Count >= targetComp.GetMaxSockets())
        {
            failReason = $"[Lv.{targetComp.CurrentMark}] 硬件接口已满 ({targetComp.GetMaxSockets()}/{targetComp.GetMaxSockets()})";
            return false;
        }
        // --- 1. 大类校验 (Component Type) ---
        if (!chipSO.AllowedTypes.Contains(targetComp.BaseData.Type))
        {
            failReason = $"契约冲突：该芯片不支持 [{targetComp.BaseData.Type}] 类零件。";
            return false;
        }

        // --- 2. 投递方式匹配 (Delivery Mode) ---
        // 只有零件是武器时才需要检查投递匹配
        if (chipSO.LimitByDelivery && targetComp.BaseData.Type == ComponentType.Weapon)
        {
            if (chipSO.RequiredDelivery != targetComp.BaseData.DeliveryType)
            {
                failReason = $"载体不匹配：芯片需要 [{chipSO.RequiredDelivery}]，但零件为 [{targetComp.BaseData.DeliveryType}]。";
                return false;
            }
        }

        // --- 3. 标签亲和力 (Tag Affinity) ---
        // 如果芯片要求特定标签，零件必须具备其中之一
        if (chipSO.RequiredTags.Count > 0)
        {
            bool hasMatchingTag = false;
            foreach (var reqTag in chipSO.RequiredTags)
            {
                if (targetComp.BaseData.BaseSubTags.Contains(reqTag))
                {
                    hasMatchingTag = true;
                    break;
                }
            }

            if (!hasMatchingTag)
            {
                failReason = "标签排斥：该零件缺乏激活芯片所需的底层协议标签。";
                return false;
            }
        }

        return true; // 审计通过
    }
}