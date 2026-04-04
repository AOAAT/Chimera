// --- START OF FILE DamagePopupManager.cs ---
using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance;

    [Tooltip("需要挂载 TextMeshPro 组件的预制体")]
    public GameObject PopupPrefab;

    private void Awake() { if (Instance == null) Instance = this; }

    public void SpawnPopup(Vector3 position, float amount, bool isCrit, bool isTrueDamage, bool isArmorAbsorb, bool isPlayer)
    {
        if (PopupPrefab == null || amount <= 0.1f) return;

        GameObject popupObj = Instantiate(PopupPrefab, position, Quaternion.identity);
        DamagePopup popup = popupObj.GetComponent<DamagePopup>();
        if (popup != null)
        {
            popup.Setup(amount, isCrit, isTrueDamage, isArmorAbsorb, isPlayer);
        }
    }
}