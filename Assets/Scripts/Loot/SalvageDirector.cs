using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// 这是处理掉落算法的纯净后端 (UI部分后续我再重构)
public class SalvageDirector : MonoBehaviour
{
    public static SalvageDirector Instance;
    public SalvageConfigSO Config;

    private void Awake() { if (Instance == null) Instance = this; }

    // ==========================================
    // 阶段 1：根据大地图阵营，生成 3 个细分标签
    // ==========================================
    public List<SubTag> GenerateStage1Tags(MacroCategory nodeCategory, int count = 3)
    {
        var inventoryMgr = PlayerInventoryManager.Instance;

        // 1. 物理隔离：只捞这个阵营大类的图纸
        var validComps = inventoryMgr.AllComponentDatabase.Where(c => c.MacroCategory == nodeCategory).ToList();
        var validChassis = inventoryMgr.AllChassisDatabase.Where(c => c.MacroCategory == nodeCategory).ToList();

        // 2. 收集池子里所有存在的细分标签 (去重)
        HashSet<SubTag> availableTags = new HashSet<SubTag>();
        foreach (var c in validComps) foreach (var t in c.BaseSubTags) availableTags.Add(t);
        foreach (var c in validChassis) foreach (var t in c.SubTags) availableTags.Add(t);

        // 3. 洗牌并返回指定数量
        return availableTags.OrderBy(x => System.Guid.NewGuid()).Take(count).ToList();
    }

    // ==========================================
    // 阶段 2：玩家选定标签后，生成具体物品选项
    // ==========================================
    public class SalvageResult
    {
        public SalvageDropType DropType;
        public List<InstancedComponent> Components = new List<InstancedComponent>();
        public List<InstancedChassis> Chassis = new List<InstancedChassis>();
    }

    public SalvageResult GenerateStage2Items(SubTag selectedTag, int currentDepth)
    {
        SalvageResult result = new SalvageResult();
        var inventoryMgr = PlayerInventoryManager.Instance;

        // 1. 决定发牌模式 (单选 or 三选一)
        float roll = UnityEngine.Random.value;
        result.DropType = roll <= Config.SingleDropChance ? SalvageDropType.SingleDrop : SalvageDropType.DraftThree;
        int targetCount = result.DropType == SalvageDropType.SingleDrop ? 1 : 3;

        // 2. 二次过滤：死死咬住玩家选定的那个标签！
        var validComps = inventoryMgr.AllComponentDatabase.Where(c => c.BaseSubTags.Contains(selectedTag)).ToList();
        var validChassis = inventoryMgr.AllChassisDatabase.Where(c => c.SubTags.Contains(selectedTag)).ToList();

        for (int i = 0; i < targetCount; i++)
        {
            // 在底盘和组件之间随便 Roll 一下 (这里可以扩展权重)
            bool rollComponent = UnityEngine.Random.value > 0.2f && validComps.Count > 0;

            if (rollComponent)
            {
                var chosenSO = validComps[UnityEngine.Random.Range(0, validComps.Count)];
                int rolledLevel = RollLevelForBlueprint(chosenSO, currentDepth);
                result.Components.Add(new InstancedComponent(chosenSO, rolledLevel));
            }
            else if (validChassis.Count > 0)
            {
                var chosenSO = validChassis[UnityEngine.Random.Range(0, validChassis.Count)];
                result.Chassis.Add(new InstancedChassis(chosenSO));
            }
        }
        return result;
    }

    private int RollLevelForBlueprint(ComponentDataSO blueprint, int currentDepth)
    {
        int w1 = Config.GetWeightForLevel(currentDepth, 1);
        int w2 = Config.GetWeightForLevel(currentDepth, 2);
        int w3 = Config.GetWeightForLevel(currentDepth, 3);

        if (blueprint.MinDropLevel > 1) w1 = 0;
        if (blueprint.MinDropLevel > 2) w2 = 0;

        int totalW = w1 + w2 + w3;
        if (totalW <= 0) return blueprint.MinDropLevel; // 保护

        int roll = UnityEngine.Random.Range(0, totalW);
        if (roll < w1) return 1;
        if (roll < w1 + w2) return 2;
        return 3;
    }
}