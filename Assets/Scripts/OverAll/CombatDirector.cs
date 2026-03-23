using UnityEngine;
using UnityEngine.UI;

public class CombatDirector : MonoBehaviour
{
    public static CombatDirector Instance { get; private set; }

    [Header("=== UI 层引用 (Canvas里) ===")]
    public GameObject CombatUIPanel;
    public Button StartBattleButton;

    [Header("=== 真实世界引用 (Canvas外) ===")]
    public Transform EnemySpawnWorldPoint; // 刚才拖到 Canvas 外面的那个空物体
    public GameObject TestEnemyPrefab;

    [Header("=== 部署配置 ===")]
    public float SpawnSpreadRadius = 2f; // 真实世界的 2 米散开半径

    private MapNodeData currentNodeData;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    private void Start()
    {
        if (StartBattleButton != null)
            StartBattleButton.onClick.AddListener(OnBattleStartClicked);
    }

    public void EnterCombatPhase(MapNodeData nodeData)
    {
        currentNodeData = nodeData;

        // 1. 弹起 UI 遮罩
        CombatUIPanel.SetActive(true);
        StartBattleButton.interactable = true;

        // 2. 在真实世界生成怪物
        SpawnEnemiesForDeployment(nodeData);
    }
    private void SpawnEnemiesForDeployment(MapNodeData nodeData)
    {
        // 1. 清理上一局留在真实世界的残骸
        foreach (Transform child in EnemySpawnWorldPoint) Destroy(child.gameObject);

        int enemyCount = (nodeData.NodeType == MapNodeType.Elite) ? 3 : 2;

        for (int i = 0; i < enemyCount; i++)
        {
            // 2. 实例化到真实世界
            GameObject enemy = Instantiate(TestEnemyPrefab, EnemySpawnWorldPoint);

            // 3. 👇【核心修正：视觉尺寸彻底独立！】
            // 既然 Sandbox 里不管视觉大小，我们就保持预制体原本气宇轩昂的 1:1 体格！
            enemy.transform.localScale = TestEnemyPrefab.transform.localScale;

            // 4. 👇【视觉空间散开】：直接给一个视觉上的散开距离（比如 1.5 米）
            // 绝对不能再去乘那个 0.08 的逻辑距离了，否则它们会叠罗汉挤在一起！
            Vector2 randomVisualOffset = Random.insideUnitCircle * 1.5f;
            Vector3 finalPos = new Vector3(randomVisualOffset.x, randomVisualOffset.y, 0f);

            enemy.transform.localPosition = finalPos;

            Debug.Log($"【战斗导演】修正完毕！第 {i + 1} 只敌人就位，视觉大小已摆脱 Sandbox 限制！");
        }
    }
    private void OnBattleStartClicked()
    {
        StartBattleButton.interactable = false;
        Debug.Log("【战斗导演】引擎轰鸣！真实物理世界，全军出击！");
        // TODO: 激活 AI
    }

    public void EndCombat(bool isVictory)
    {
        CombatUIPanel.SetActive(false);
        if (isVictory) MapManager.Instance.OnCombatVictory(currentNodeData);
    }
}