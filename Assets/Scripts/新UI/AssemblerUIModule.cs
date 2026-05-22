using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AssemblerUIModule : MonoBehaviour
{
    [Header("=== 按钮引用 ===")]
    public Button EnterWorkshopButton;

    private AssemblerBuilding bindedBuilding;

    /// <summary>
    /// 初始化：将 UI 按钮与具体的组装厂实例绑定
    /// </summary>
    public void Initialize(AssemblerBuilding building)
    {
        bindedBuilding = building;

        if (EnterWorkshopButton != null && bindedBuilding != null)
        {
            EnterWorkshopButton.onClick.RemoveAllListeners();
            EnterWorkshopButton.onClick.AddListener(() => {
                bindedBuilding.OpenWorkshop();
            });
        }
    }
}