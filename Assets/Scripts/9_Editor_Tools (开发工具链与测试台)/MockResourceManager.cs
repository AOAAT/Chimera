using UnityEngine;

// 模拟资源管理器：提供全局数据接口
public static class MockResourceManager
{
    // 你可以在任何测试脚本里修改这个值，比如在 TestEnemy 里按空格键增加电能
    public static float SurplusPower = 50f;

    // 对外暴露的获取接口
    public static float GetSurplusPower()
    {
        return SurplusPower;
    }
}