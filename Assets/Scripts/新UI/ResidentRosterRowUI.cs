using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResidentRosterRowUI : MonoBehaviour
{
    public TMP_Text NameText;
    public TMP_Text MetaText;
    public TMP_Text DetailText;
    public Button ActionButton;
    public TMP_Text ActionText;

    public void Bind(string residentName, string meta, string detail, string actionLabel,
        bool actionEnabled, Action action)
    {
        if (NameText != null) NameText.text = residentName;
        if (MetaText != null) MetaText.text = meta;
        if (DetailText != null) DetailText.text = detail;
        if (ActionText != null) ActionText.text = actionLabel;
        if (ActionButton == null) return;

        ActionButton.interactable = actionEnabled;
        ActionButton.onClick.RemoveAllListeners();
        if (action != null) ActionButton.onClick.AddListener(() => action());
    }
}
