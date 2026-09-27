using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class AssemblyInteractionChecks
{
    private const string Key = "AssemblyInteractionChecks";
    private static string Output => Path.Combine(Directory.GetCurrentDirectory(), "AssemblyResults");
    private static double started, next;
    private static int stage, coreSlot, moveSlot, chassisBefore, mechBefore;
    private static ChassisDataSO chassis;
    private static ComponentDataSO core, movement;
    private static AssemblyWorkshopUI workshop;
    private static AssemblerBuilding assembler;
    private static string unitID, coreID, moveID;
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
        File.AppendAllText(Path.Combine(Output, "results.txt"), "PASS " + message + "\n");
    }
    public static void Run()
    {
        Directory.CreateDirectory(Output); File.WriteAllText(Path.Combine(Output, "results.txt"), "");
        EditorSceneManager.OpenScene("Assets/Scenes/RTS_World_Master.unity");
        SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
    }
    [InitializeOnLoadMethod]
    private static void Resume()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup; next = started + 6;
        EditorApplication.update += Tick;
    }
    private static void Click(GameObject target)
    {
        Canvas.ForceUpdateCanvases();
        var rect = target.GetComponent<RectTransform>();
        var canvas = target.GetComponentInParent<Canvas>();
        Vector2 position = RectTransformUtility.WorldToScreenPoint(canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            rect.TransformPoint(rect.rect.center));
        var data = new PointerEventData(EventSystem.current) { position = position, button = PointerEventData.InputButton.Left };
        var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(data, hits);
        var handler = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
        Check(handler != null && (handler == target || handler.transform.IsChildOf(target.transform)),
            "pointer reaches " + target.name + " (top hit: " + (hits.Count > 0 ? hits[0].gameObject.name : "none") + ")");
        data.pointerCurrentRaycast = hits[0];
        ExecuteEvents.Execute(handler, data, ExecuteEvents.pointerClickHandler);
    }
    private static Button Action(string method) => workshop.GetComponentsInChildren<Button>(true).First(b =>
        Enumerable.Range(0, b.onClick.GetPersistentEventCount()).Any(i => b.onClick.GetPersistentMethodName(i) == method));
    private static InventoryItemSlotUI ChassisCard() => RightInventoryPanelUI.Instance.ContentRoot.GetComponentsInChildren<InventoryItemSlotUI>()
        .First(x => Field<InstancedChassis>(x, "cachedChassis")?.BaseData == chassis);
    private static InventoryItemSlotUI ComponentCard(ComponentDataSO definition) => RightInventoryPanelUI.Instance.ContentRoot.GetComponentsInChildren<InventoryItemSlotUI>()
        .First(x => Field<InstancedComponent>(x, "cachedComponent")?.BaseData == definition);
    private static GameObject Socket(int index) => workshop.ChassisVisualRoot.GetComponentsInChildren<Button>()
        .First(b => b.name == "UI_Socket_" + chassis.Sockets[index].SlotName).gameObject;
    private static int ChassisCount() => PlayerInventoryManager.Instance.GetChassisStacks().Where(x => x.BaseData == chassis).Sum(x => x.Quantity);
    private static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
        next = EditorApplication.timeSinceStartup + .65;
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Timed out");
            var inventory = PlayerInventoryManager.Instance;
            switch (stage++)
            {
                case 0:
                    if (LogisticsManager.Instance == null || !LogisticsManager.Instance.Ready) { stage--; return; }
                    assembler = Object.FindObjectOfType<AssemblerBuilding>();
                    workshop = Object.FindObjectOfType<AssemblyWorkshopUI>(true);
                    chassis = inventory.AllChassisDatabase.First(x => x.Sockets.Any(s => s.AllowedTypes.Contains(ComponentType.Core)) &&
                        x.Sockets.Any(s => s.AllowedTypes.Contains(ComponentType.Movement)));
                    coreSlot = chassis.Sockets.FindIndex(x => x.AllowedTypes.Contains(ComponentType.Core));
                    moveSlot = chassis.Sockets.FindIndex(x => x != chassis.Sockets[coreSlot] && x.AllowedTypes.Contains(ComponentType.Movement));
                    core = inventory.AllComponentDatabase.First(x => x.Type == ComponentType.Core);
                    movement = inventory.AllComponentDatabase.First(x => x.Type == ComponentType.Movement);
                    inventory.AddChassisToWarehouse(chassis, 2); inventory.AddComponentToWarehouse(core, 1, 2); inventory.AddComponentToWarehouse(movement, 1, 2);
                    chassisBefore = ChassisCount(); mechBefore = Object.FindObjectsOfType<MechUnit2D>().Length;
                    SelectionContextHUD.Instance.Refresh(assembler);
                    Check(assembler.FunctionUIPrefab != null, "assembler retains functional UI prefab binding"); break;
                case 1:
                    Click(Object.FindObjectOfType<AssemblerUIModule>().EnterWorkshopButton.gameObject);
                    Check(workshop.gameObject.activeInHierarchy, "workshop opens from building button"); break;
                case 2:
                    Click(workshop.GhostChassisPrompt);
                    Check(RightInventoryPanelUI.Instance.gameObject.activeInHierarchy, "chassis selector opens"); break;
                case 3:
                    var chassisCard = ChassisCard();
                    ChimeraUITheme.ApplyPanel(chassisCard.gameObject, false);
                    Check(chassisCard.GetComponent<Image>().raycastTarget && !chassisCard.ItemIcon.raycastTarget,
                        "reapplying theme preserves card hit area and decorative icon passthrough");
                    Click(chassisCard.gameObject);
                    Check(Field<SavedUnitProfile>(workshop, "currentEditingProfile")?.ChassisData == chassis, "click selects chassis");
                    Check(ChassisCount() == chassisBefore - 1, "selecting chassis consumes exactly one warehouse item"); break;
                case 4: Click(Socket(coreSlot)); break;
                case 5:
                    var card = ComponentCard(core); coreID = Field<InstancedComponent>(card, "cachedComponent").InstanceID; Click(card.gameObject);
                    Check(Field<SavedUnitProfile>(workshop, "currentEditingProfile").EquippedComponentIDs.Contains(coreID), "click installs chosen core instance"); break;
                case 6: Click(Socket(moveSlot)); break;
                case 7:
                    var movingCard = ComponentCard(movement); moveID = Field<InstancedComponent>(movingCard, "cachedComponent").InstanceID; Click(movingCard.gameObject);
                    Check(Field<SavedUnitProfile>(workshop, "currentEditingProfile").EquippedComponentIDs.Contains(moveID), "click installs chosen movement instance"); break;
                case 8:
                    var slots=new InstancedComponent[chassis.Sockets.Count];slots[coreSlot]=inventory.GetComponentInstance(coreID);
                    var alternate=new InstancedComponent(core,1);
                    alternate.RolledStats.Add(new StatEntry{StatID=StatType.AddedHP,Value=123,ModType=BuffModifierType.Additive});
                    var replacement=AssemblyComparison.Evaluate(chassis,slots,coreSlot,alternate);
                    var cleanInstall=AssemblyComparison.Evaluate(chassis,new InstancedComponent[chassis.Sockets.Count],coreSlot,alternate);
                    Check(replacement.After.SequenceEqual(cleanInstall.After)&&slots[coreSlot].InstanceID==coreID,"replacing with a different component removes old contributions and preserves equipped instance");
                    Check(workshop.ChassisVisualRoot.GetComponentsInChildren<Image>().Any(x=>x.name=="Sprite_Visual" && x.sprite==core.ComponentIcon) &&
                        workshop.ChassisVisualRoot.GetComponentsInChildren<Image>().Any(x=>x.name=="Sprite_Visual" && x.sprite==movement.ComponentIcon),
                        "preview appearance changes to actual installed component sprites");
                    IvoryThemeChecks.Capture(workshop.GetComponentInParent<Canvas>(),"assembly.png");
                    unitID = Field<SavedUnitProfile>(workshop, "currentEditingProfile").UnitID;
                    Click(Action("SaveAndExitWorkshop").gameObject);
                    Check(!workshop.gameObject.activeSelf, "valid assembly closes workshop"); break;
                case 9:
                    Check(Object.FindObjectsOfType<MechUnit2D>().Length == mechBefore + 1, "confirmation spawns exactly one world mech");
                    var mech = Object.FindObjectsOfType<MechUnit2D>().First(x => x.GetProfile()?.UnitID == unitID);
                    Check(mech.GetProfile().EquippedComponentIDs.Contains(coreID) && mech.GetProfile().EquippedComponentIDs.Contains(moveID), "spawned mech retains chosen component identities");
                    Check(inventory.GetComponentInstance(coreID).EquippedUnitID == unitID && !LogisticsManager.Instance.ItemAvailable("component:" + coreID), "equipped component cannot be reused from warehouse");
                    UnitDetailPanelUI.Instance.OpenDetail(mech); break;
                case 10:
                    var detailVisual=UnitDetailPanelUI.Instance.UnitVisualContainer.GetComponentsInChildren<Image>().First(x=>x.sprite==core.ComponentIcon);
                    ExecuteEvents.Execute(detailVisual.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);break;
                case 11:
                    Check(ItemHoverTooltip.Instance!=null&&ItemHoverTooltip.Instance.Visible&&ItemHoverTooltip.Instance.TitleText==inventory.GetComponentInstance(coreID).DisplayName,"unit inspection uses shared tooltip for installed component");
                    Check(!Object.FindObjectsOfType<Transform>(true).Any(x=>x.name.Contains("ItemDetailPanelUI")),"legacy panel is absent after inspecting a deployed mech");
                    UnitDetailPanelUI.Instance.CloseDetail();
                    Check(!ItemHoverTooltip.Instance.Visible,"closing unit inspection hides shared tooltip");
                    assembler.OpenWorkshop();break;
                case 12: Click(workshop.GhostChassisPrompt); break;
                case 13: Click(ChassisCard().gameObject); break;
                case 14:
                    Click(Action("CancelAndExitWorkshop").gameObject);
                    Check(ChassisCount() == chassisBefore - 1, "cancelling next assembly returns its chassis exactly once");
                    Check(Object.FindObjectsOfType<MechUnit2D>().Length == mechBefore + 1, "cancel does not spawn another mech");
                    Finish(0); break;
            }
        }
        catch (Exception ex) { File.AppendAllText(Path.Combine(Output, "results.txt"), "FAIL " + ex + "\n"); Finish(1); }
    }
    private static void Finish(int code) { SessionState.SetBool(Key, false); EditorApplication.update -= Tick; EditorApplication.Exit(code); }
}
