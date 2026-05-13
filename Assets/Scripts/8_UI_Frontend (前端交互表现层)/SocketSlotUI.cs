using UnityEngine;
using UnityEngine.UI;
using System;

public class SocketSlotUI : MonoBehaviour
{
    public Image AccessoryIcon;   // 芯片的小图
    public GameObject SelectionFrame; // 选中时的光圈
    public GameObject EmptyIcon; // 没装东西时的虚线圈

    private int myIndex;
    private Action<int> onClickCallback;

    public void Initialize(int index, InstancedAccessory accessory, bool isSelected, Action<int> onClick)
    {
        myIndex = index;
        onClickCallback = onClick;

        if (SelectionFrame != null) SelectionFrame.SetActive(isSelected);

        if (accessory != null)
        {
            AccessoryIcon.gameObject.SetActive(true);
            AccessoryIcon.sprite = accessory.BaseData.AccessoryIcon;
            if (EmptyIcon != null) EmptyIcon.SetActive(false);
        }
        else
        {
            AccessoryIcon.gameObject.SetActive(false);
            if (EmptyIcon != null) EmptyIcon.SetActive(true);
        }

        GetComponent<Button>().onClick.RemoveAllListeners();
        GetComponent<Button>().onClick.AddListener(() => onClickCallback?.Invoke(myIndex));
    }
}