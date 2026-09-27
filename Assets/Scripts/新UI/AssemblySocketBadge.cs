using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class AssemblySocketBadge : MonoBehaviour
{
    public int SlotIndex;
    public bool Occupied;
    private Image marker;
    private TMP_Text number;
    public void Initialize(int slot,bool occupied,Sprite sprite,TMP_FontAsset font)
    {
        SlotIndex=slot;Occupied=occupied;
        marker=new GameObject("NodeState",typeof(RectTransform),typeof(Image),typeof(Outline)).GetComponent<Image>();
        marker.transform.SetParent(transform,false);marker.sprite=sprite;marker.raycastTarget=false;
        marker.transform.localRotation=Quaternion.Inverse(transform.localRotation);
        marker.rectTransform.sizeDelta=new Vector2(12,12);marker.rectTransform.anchoredPosition=new Vector2(7,7);
        number=new GameObject("NodeNumber",typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TMP_Text>();
        number.transform.SetParent(marker.transform,false);number.font=ChimeraUITheme.Config.Font??font;
        number.text=(slot+1).ToString();number.fontSize=8;number.alignment=TextAlignmentOptions.Center;number.raycastTarget=false;
        number.rectTransform.anchorMin=Vector2.zero;number.rectTransform.anchorMax=Vector2.one;
        number.rectTransform.offsetMin=number.rectTransform.offsetMax=Vector2.zero;
        SetSelected(false);
    }
    public void SetSelected(bool selected)
    {
        if(marker==null)return;
        marker.color=selected?ChimeraUITheme.Accent:Occupied?ChimeraUITheme.HP:ChimeraUITheme.Button;
        var socket=GetComponent<Image>();
        if(socket!=null&&!Occupied)socket.color=marker.color;
        number.color=selected||Occupied?ChimeraUITheme.Config.OnAccent:ChimeraUITheme.PrimaryText;
        var outline=marker.GetComponent<Outline>();outline.effectColor=selected?ChimeraUITheme.PrimaryText:ChimeraUITheme.Config.Border;
        outline.effectDistance=Vector2.one*(selected?1:.4f);
    }
}
