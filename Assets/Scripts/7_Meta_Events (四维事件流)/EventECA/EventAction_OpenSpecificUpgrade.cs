// --- EventAction_OpenSpecificUpgrade.cs ---
using UnityEngine;

[CreateAssetMenu(fileName = "Act_ForceUpgrade", menuName = "Chimera Protocol/Event ECA/打开强化界面")]
public class EventAction_OpenSpecificUpgrade : EventAction
{
    public override void Execute()
    {
        // 1. 关闭事件面板
        EventDirector.Instance.EventPanel.SetActive(false);

        // 2. 模拟点击“电焊车间”
        // 这里需要你让 AssemblyWorkshopUI 暴露一个特殊的“免费升级模式”接口
        Debug.Log("【系统】神秘光球正在共振，你可以免费加固一件零件！");
        // AssemblyWorkshopUI.Instance.OpenForFreeUpgrade();
    }
}