using UnityEngine;

public class HousingBuilding : BuildingBase
{
    [Header("=== 人口贡献 ===")]
    [Tooltip("这座建筑能提供多少人口上限？")]
    public int CapacityProvided = 2;

    public override void OnPlaced()
    {
        base.OnPlaced();

        // 放置成功后，通知人口管理器重新计算上限
        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.RefreshMaxCapacity();
        }

        Debug.Log($"<color=cyan>【城市规划】</color> {BuildingName} 已竣工，基地人口配额增加 {CapacityProvided}。");
    }

    private void OnDestroy()
    {
        // 如果建筑被摧毁（未来可能有战斗损毁或拆除），需要扣除上限
        if (PopulationManager.Instance != null)
        {
            PopulationManager.Instance.RefreshMaxCapacity();
        }
    }
}