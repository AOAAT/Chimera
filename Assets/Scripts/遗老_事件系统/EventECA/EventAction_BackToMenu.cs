// --- EventAction_BackToMenu.cs ---
using UnityEngine;
using UnityEngine.SceneManagement;

[CreateAssetMenu(fileName = "Act_BackToMenu", menuName = "Chimera Protocol/Event ECA/返回主菜单")]
public class EventAction_BackToMenu : EventAction
{
    public override void Execute()
    {
        Debug.Log("【系统】Demo 流程结束，正在清理环境并返回...");

        // 1. 执行全场大清扫
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.FullResetBeforeExit();

        // 2. 强制确保时间流速正常
        Time.timeScale = 1f;

        // 3. 卸载游戏场景，加载主菜单（Scene 0）
        SceneManager.LoadScene(0);
    }
}