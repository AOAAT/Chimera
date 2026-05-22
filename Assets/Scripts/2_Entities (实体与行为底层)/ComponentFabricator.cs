using UnityEngine;

public class ComponentFabricator : MonoBehaviour
{
    public static ComponentFabricator Instance;

    private void Awake() => Instance = this;

    /// <summary>
    /// 外部 UI 按钮调用的制造请求
    /// </summary>
    public void RequestFabrication(ComponentDataSO targetSO, int targetLevel)
    {
        // 预留接口：资源校验
        if (!CheckCost(targetSO, targetLevel)) return;

        // 预留接口：生产时间逻辑 (目前设为 0，即瞬时产出)
        float processTime = GetProductionTime(targetSO, targetLevel);

        if (processTime <= 0)
        {
            PlayerInventoryManager.Instance.AddComponentToWarehouse(targetSO, targetLevel, 1);
        }
        else
        {
            // 这里未来可以接入协程计时
            Debug.Log($"零件 {targetSO.ComponentName} 开始生产，需等待 {processTime}s");
        }
    }

    private bool CheckCost(ComponentDataSO so, int lv)
    {
        // TODO: 在此接入资源判定
        return true;
    }

    private float GetProductionTime(ComponentDataSO so, int lv)
    {
        // TODO: 在此接入时间计算
        return 0f;
    }
}