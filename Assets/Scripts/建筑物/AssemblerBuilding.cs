using UnityEngine;

public class AssemblerBuilding : BuildingBase
{
    [Header("=== 组装特有属性 ===")]
    public float AssemblerLevel = 1.0f;

    protected override void Awake()
    {
        base.Awake();
        // 可以在这里初始化组装厂特有的逻辑（如生产队列）
    }

    public void OpenWorkshop()
    {
        // 以后在这里呼叫全屏 UI
        Debug.Log($"<color=cyan>【组装中心】</color> {BuildingName} 正在开启工坊...");
    }
}