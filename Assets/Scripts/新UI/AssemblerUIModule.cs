using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AssemblerUIModule : MonoBehaviour
{
    public Button EnterWorkshopButton;
    private AssemblerBuilding bindedBuilding;

    public void Initialize(AssemblerBuilding building)
    {
        bindedBuilding = building;

        if (EnterWorkshopButton != null)
        {
            EnterWorkshopButton.onClick.RemoveAllListeners();
            EnterWorkshopButton.onClick.AddListener(() => {
                // 🌟 调用组装厂自己的方法，间接打开车间
                if (bindedBuilding != null) bindedBuilding.OpenWorkshop();
            });
        }
    }
}