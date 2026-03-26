using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class RewardEntryUI : MonoBehaviour
{
    public TMP_Text TitleText;
    public TMP_Text DescText;
    public Button ClickButton;
    public GameObject ClaimedOverlay; // 领取后变灰打勾的遮罩

    private int myIndex;
    private Action<int> onClickCallback;

    public void Initialize(int index, string title, string desc, bool isClaimed, Action<int> callback)
    {
        myIndex = index;
        TitleText.text = title;
        DescText.text = desc;
        onClickCallback = callback;

        if (ClaimedOverlay != null) ClaimedOverlay.SetActive(isClaimed);
        ClickButton.interactable = !isClaimed;

        ClickButton.onClick.RemoveAllListeners();
        ClickButton.onClick.AddListener(() => onClickCallback?.Invoke(myIndex));
    }
}