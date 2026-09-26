using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(900)]
public class ChimeraUIThemeController : MonoBehaviour
{
    private static ChimeraUIThemeController instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null) return;
        GameObject controller = new GameObject("Chimera_UI_Theme");
        instance = controller.AddComponent<ChimeraUIThemeController>();
        DontDestroyOnLoad(controller);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        instance = null;
    }

    private void Start()
    {
        Debug.Log("<color=#5CBE91>[UI主题]</color> 已启用统一界面主题。");
        StartCoroutine(ThemeRefreshLoop());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyAfterLayout());
    }

    private IEnumerator ApplyAfterLayout()
    {
        yield return null;
        ApplyKnownInterfaces();
    }

    private IEnumerator ThemeRefreshLoop()
    {
        while (true)
        {
            ApplyKnownInterfaces();
            yield return new WaitForSecondsRealtime(0.75f);
        }
    }

    private static void ApplyKnownInterfaces()
    {
        foreach (SelectionContextHUD hud in FindSceneObjects<SelectionContextHUD>())
        {
            ChimeraUITheme.ApplyPanel(hud.gameObject);
            ChimeraUITheme.ApplyPanel(hud.BuildingRoot);
            ChimeraUITheme.ApplyPanel(hud.MechRoot);
            ChimeraUITheme.ApplyPanel(hud.ResidentRoot);
        }
        foreach (GlobalResourceHUD hud in FindSceneObjects<GlobalResourceHUD>())
            ChimeraUITheme.ApplyPanel(hud.gameObject);
        foreach (GlobalWarehouseUI warehouse in FindSceneObjects<GlobalWarehouseUI>())
            ChimeraUITheme.ApplyPanel(warehouse.gameObject);
        foreach (RightInventoryPanelUI inventory in FindSceneObjects<RightInventoryPanelUI>())
            ChimeraUITheme.ApplyPanel(inventory.gameObject);
        foreach (AssemblyWorkshopUI workshop in FindSceneObjects<AssemblyWorkshopUI>())
        {
            ChimeraUITheme.ApplyPanel(workshop.gameObject, false);
            if (workshop.LeftStatsPanel != null) ChimeraUITheme.ApplyPanel(workshop.LeftStatsPanel.gameObject);
            if (workshop.CenterPreviewArea != null) ChimeraUITheme.ApplyPanel(workshop.CenterPreviewArea.gameObject);
            if (workshop.RightInventoryPanel != null) ChimeraUITheme.ApplyPanel(workshop.RightInventoryPanel.gameObject);
        }
        foreach (PauseMenuUI pause in FindSceneObjects<PauseMenuUI>())
            ChimeraUITheme.ApplyPanel(pause.PausePanel);
        foreach (MainMenuUI menu in FindSceneObjects<MainMenuUI>())
            ChimeraUITheme.ApplyPanel(menu.gameObject, false);
        foreach (UnitDetailPanelUI detail in FindSceneObjects<UnitDetailPanelUI>())
            ChimeraUITheme.ApplyPanel(detail.gameObject);
        foreach (ItemDetailPanelUI itemDetail in FindSceneObjects<ItemDetailPanelUI>())
            ChimeraUITheme.ApplyPanel(itemDetail.gameObject, false, false);

        // 场景中还有仓库、建造与暂停等独立按钮，不属于上述任何面板。
        foreach (UnityEngine.UI.Button button in FindSceneObjects<UnityEngine.UI.Button>())
            ChimeraUITheme.StyleButton(button);
    }

    private static T[] FindSceneObjects<T>() where T : Component
    {
        T[] objects = Resources.FindObjectsOfTypeAll<T>();
        return System.Array.FindAll(objects, item =>
            item != null && item.gameObject.scene.IsValid() && !item.gameObject.scene.name.Equals("DontDestroyOnLoad"));
    }
}
