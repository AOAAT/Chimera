using UnityEngine;

[DefaultExecutionOrder(900)]
public class ChimeraUIThemeController : MonoBehaviour
{
    // Explicit authoring operation: no periodic scanning or runtime style overrides.
    public static void ApplyToRoot(GameObject root)
    {
        foreach (SelectionContextHUD hud in FindInRoot<SelectionContextHUD>(root))
        {
            ChimeraUITheme.ApplyPanel(hud.gameObject);
            ChimeraUITheme.ApplyPanel(hud.BuildingRoot);
            ChimeraUITheme.ApplyPanel(hud.MechRoot);
            ChimeraUITheme.ApplyPanel(hud.ResidentRoot);
        }
        foreach (GlobalResourceHUD hud in FindInRoot<GlobalResourceHUD>(root))
            ChimeraUITheme.ApplyPanel(hud.gameObject);
        foreach (GlobalWarehouseUI warehouse in FindInRoot<GlobalWarehouseUI>(root))
            ChimeraUITheme.ApplyPanel(warehouse.gameObject);
        foreach (RightInventoryPanelUI inventory in FindInRoot<RightInventoryPanelUI>(root))
            ChimeraUITheme.ApplyPanel(inventory.gameObject);
        foreach (AssemblyWorkshopUI workshop in FindInRoot<AssemblyWorkshopUI>(root))
        {
            ChimeraUITheme.ApplyPanel(workshop.gameObject, false);
            if (workshop.LeftStatsPanel != null) ChimeraUITheme.ApplyPanel(workshop.LeftStatsPanel.gameObject);
            if (workshop.CenterPreviewArea != null) ChimeraUITheme.ApplyPanel(workshop.CenterPreviewArea.gameObject);
            if (workshop.RightInventoryPanel != null) ChimeraUITheme.ApplyPanel(workshop.RightInventoryPanel.gameObject);
        }
        foreach (PauseMenuUI pause in FindInRoot<PauseMenuUI>(root))
            ChimeraUITheme.ApplyPanel(pause.PausePanel);
        foreach (MainMenuUI menu in FindInRoot<MainMenuUI>(root))
            ChimeraUITheme.ApplyPanel(menu.gameObject, false);
        foreach (UnitDetailPanelUI detail in FindInRoot<UnitDetailPanelUI>(root))
            ChimeraUITheme.ApplyPanel(detail.gameObject);
        foreach (ItemDetailPanelUI itemDetail in FindInRoot<ItemDetailPanelUI>(root))
            ChimeraUITheme.ApplyPanel(itemDetail.gameObject, false, false);

        // 场景中还有仓库、建造与暂停等独立按钮，不属于上述任何面板。
        foreach (UnityEngine.UI.Button button in FindInRoot<UnityEngine.UI.Button>(root))
            ChimeraUITheme.StyleButton(button);
    }

    private static T[] FindInRoot<T>(GameObject root) where T : Component
    {
        return root.GetComponentsInChildren<T>(true);
    }
}
