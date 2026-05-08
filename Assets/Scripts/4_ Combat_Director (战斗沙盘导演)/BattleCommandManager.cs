using UnityEngine;
using UnityEngine.EventSystems;

public class BattleCommandManager : MonoBehaviour
{
    public static BattleCommandManager Instance;

    [Header("=== 选中设置 ===")]
    public ChimeraAIController SelectedUnit;
    public GameObject ClickVFXPrefab; // 绿色的收缩箭头预制体

    private LineRenderer targetingLine;

    private void Awake()
    {
        Instance = this;
        // 初始化红色的火控虚线
        targetingLine = gameObject.AddComponent<LineRenderer>();
        targetingLine.startWidth = targetingLine.endWidth = 0.04f;
        targetingLine.material = new Material(Shader.Find("Sprites/Default"));
        targetingLine.startColor = new Color(1, 0, 0, 0.7f);
        targetingLine.endColor = new Color(1, 0, 0, 0.1f);
        targetingLine.enabled = false;
        targetingLine.sortingLayerName = "UI"; // 确保在线条最上层
    }

    private void Update()
    {
        // --- 👇【核心修复 1】：强制关灯逻辑 ---
        // 如果战斗不处于激活状态，强制隐藏所有战术视觉并退出
        if (CombatDirector.Instance == null || !CombatDirector.Instance.IsCombatActive)
        {
            if (targetingLine != null) targetingLine.enabled = false;

            // 如果有选中的单位，也把它的支架关掉
            if (SelectedUnit != null)
            {
                var bracket = SelectedUnit.GetComponentInChildren<TacticalBracket>(true);
                if (bracket != null) bracket.Hide();
                SelectedUnit = null; // 清空选中，防止下一场战斗残留
            }
            return;
        }

        // 原有的 UI 屏蔽和指令处理...
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;

        HandleSelection();
        HandleCommand();
        UpdateTargetingLine();
    }

    private void HandleSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // --- 1. UI 屏蔽：点击 UI 按钮时不触发选择 ---
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // --- 2. 扩容检测：同时扫描物理层和受击层，并增加 0.25 的容错半径 ---
            int selectionMask = LayerMask.GetMask("Player_Body", "Player_Hitbox");
            Collider2D hit = Physics2D.OverlapCircle(mousePos, 0.25f, selectionMask);

            if (hit != null)
            {
                var newUnit = hit.GetComponentInParent<ChimeraAIController>();

                if (newUnit != null)
                {
                    // --- 3. 逻辑切换：如果点中了新单位 ---

                    // A. 如果之前有选中的，先把它的视觉关掉
                    if (SelectedUnit != null && SelectedUnit != newUnit)
                    {
                        GetBracket(SelectedUnit).Hide();
                        // 👈 核心：关闭旧射程圆环
                        SelectedUnit.GetComponent<WeaponRangeVisualizer>()?.SetVisible(false);
                    }

                    SelectedUnit = newUnit;

                    // B. 开启新单位的战术视觉
                    GetBracket(SelectedUnit).Show(SelectedUnit.HasManualTarget());

                    // 👈 核心：开启新射程圆环
                    var rangeViz = SelectedUnit.GetComponent<WeaponRangeVisualizer>();
                    if (rangeViz != null)
                    {
                        rangeViz.SetVisible(true);
                    }

                    PlayConfirmSound(1.0f);
                    Debug.Log($"<color=green>【火控同步】</color> 目标：{newUnit.gameObject.name}，射程网已展开。");
                    return; // 选中成功，不再走下面的清空逻辑
                }
            }

            // --- 4. 释放焦点：点击空地时，清空一切战术视觉 ---
            if (SelectedUnit != null)
            {
                GetBracket(SelectedUnit).Hide();

                // 👈 核心：清空圆环
                SelectedUnit.GetComponent<WeaponRangeVisualizer>()?.SetVisible(false);

                SelectedUnit = null;
                Debug.Log("<color=white>【火控挂起】</color> 指挥焦点已释放。");
            }
        }
    }
    private void HandleCommand()
    {
        if (SelectedUnit == null || !Input.GetMouseButtonDown(1)) return;

        Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        RaycastHit2D enemyHit = Physics2D.Raycast(mousePos, Vector2.zero, 0f, LayerMask.GetMask("Enemy_Hitbox"));

        if (enemyHit.collider != null)
        {
            // 1. 集火指令：内部会自动清除移动点
            DamageReceiver enemy = enemyHit.collider.GetComponentInParent<DamageReceiver>();
            if (enemy != null)
            {
                SelectedUnit.SetManualTarget(enemy.transform);
                GetBracket(SelectedUnit).Show(true);
                PlayConfirmSound(1.2f);
            }
        }
        else
        {
            // 2. 位移指令：内部会自动清除锁定目标，红线会自然消失
            SelectedUnit.SetManualMovePoint(mousePos);
            GetBracket(SelectedUnit).Show(false);
            PlayConfirmSound(1.0f);
        }
    }

    private void UpdateTargetingLine()
    {
        // --- 👇【核心修复 2】：多重有效性检查 ---
        if (SelectedUnit != null && SelectedUnit.HasManualTarget())
        {
            Transform target = SelectedUnit.GetManualTarget();

            // 判定：目标是否消失？目标是否死亡？目标是否已经失活？
            if (target == null || !target.gameObject.activeInHierarchy || !IsTargetAlive(target))
            {
                targetingLine.enabled = false;
                return;
            }

            targetingLine.enabled = true;
            targetingLine.SetPosition(0, SelectedUnit.transform.position);
            targetingLine.SetPosition(1, target.position);

            // 模拟雷达抖动
            targetingLine.startWidth = 0.04f + Mathf.PingPong(Time.time * 5f, 0.02f);
        }
        else
        {
            targetingLine.enabled = false;
        }
    }

    // 辅助方法：判定锁定目标是否还活着
    private bool IsTargetAlive(Transform t)
    {
        DamageReceiver dr = t.GetComponentInParent<DamageReceiver>();
        return dr != null && dr.CurrentHP > 0;
    }

    private TacticalBracket GetBracket(ChimeraAIController unit)
    {
        var b = unit.GetComponentInChildren<TacticalBracket>(true);
        if (b == null)
        {
            GameObject go = new GameObject("TacticalSelection_UI");
            go.transform.SetParent(unit.transform, false);
            b = go.AddComponent<TacticalBracket>();
        }
        return b;
    }

    private void PlayConfirmSound(float p) => Debug.Log($"<color=cyan>【指令】</color> 机甲响应中...音高: {p}");
}