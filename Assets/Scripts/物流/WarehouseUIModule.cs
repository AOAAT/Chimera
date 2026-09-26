using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class WarehouseUIModule : MonoBehaviour
{
    private WarehouseBuilding warehouse;
    private TMP_Text summary;
    public static void Create(Transform parent, WarehouseBuilding building, TMP_FontAsset font)
    {
        var go = new GameObject("WarehouseModule", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var module = go.AddComponent<WarehouseUIModule>(); module.warehouse = building;
        var text = new GameObject("StorageSummary", typeof(RectTransform), typeof(TextMeshProUGUI));
        text.transform.SetParent(go.transform, false);
        module.summary = text.GetComponent<TextMeshProUGUI>(); module.summary.font = font;
        module.summary.fontSize = 19; module.summary.color = ChimeraUITheme.PrimaryText; module.summary.raycastTarget = false;
        var textRect = module.summary.rectTransform; textRect.anchorMin = new Vector2(.03f, .15f); textRect.anchorMax = new Vector2(.7f, .85f);
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        var buttonObject = new GameObject("查看库存", typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(go.transform, false);
        var buttonRect = buttonObject.GetComponent<RectTransform>(); buttonRect.anchorMin = new Vector2(.72f, .3f); buttonRect.anchorMax = new Vector2(.98f, .7f);
        buttonRect.offsetMin = buttonRect.offsetMax = Vector2.zero;
        buttonObject.GetComponent<Image>().color = ChimeraUITheme.Button;
        buttonObject.GetComponent<Button>().onClick.AddListener(() => LogisticsPanelUI.Instance?.Open(building.PersistentID));
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI)); labelObject.transform.SetParent(buttonObject.transform, false);
        var label = labelObject.GetComponent<TextMeshProUGUI>(); label.font = font; label.fontSize = 18;
        label.text = "查看库存"; label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
        label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
    }
    private void Update()
    {
        var manager = LogisticsManager.Instance;
        if (warehouse == null || manager == null) return;
        var store = manager.Get(warehouse.PersistentID);
        if (store == null) return;
        summary.text = $"库存容量  {store.Used:0.#} / {store.Capacity:0.#}\n入库预留  {manager.ReservedIn(store.ID):0.#}\n收纳：" +
            (store.AcceptResources ? "资源 " : "") + (store.AcceptItems ? "组件与底盘" : "");
    }
}
