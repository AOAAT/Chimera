using UnityEngine;

public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    [Header("=== 测试期的兜底敌人池 ===")]
    public EncounterPoolSO TestPool;

    [Header("=== 运行时进度状态 (预留) ===")]
    public int CurrentStage = 1;
    public int CurrentNodeDepth = 1;

    private void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else { Destroy(gameObject); return; }

        // 游戏启动时，初始化洗牌
        if (TestPool != null) TestPool.InitializePool();
    }

    // 战斗导演 (CombatDirector) 只需要无脑调用这个接口，根本不用管里面怎么算的！
    public EncounterLayoutSO GetNextEncounterForCurrentNode()
    {
        // ==========================================
        // 🚀 未来的大地图逻辑扩展区：
        // if (CurrentStage == 1 && CurrentNodeDepth == 5) return Stage1_BossPool.GetNextEncounter();
        // else if (CurrentStage == 2) return Stage2_Pool.GetNextEncounter();
        // ==========================================

        // 当前测试期的兜底逻辑：永远从测试池里抽卡
        if (TestPool != null)
        {
            return TestPool.GetNextEncounter();
        }

        return null;
    }
}