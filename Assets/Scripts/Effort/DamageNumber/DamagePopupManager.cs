using UnityEngine;

public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance;

    [Tooltip("需要挂载 TextMeshPro 组件的预制体")]
    public GameObject PopupPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    public void SpawnPopup(Vector3 position, float amount, bool isCrit, bool isTrueDamage, bool isArmorAbsorb, bool isPlayer)
    {
        if (PopupPrefab == null || amount <= 0.1f) return;

        // 从对象池取出飘字
        GameObject popupObj = SimplePool.Spawn(PopupPrefab, position, Quaternion.identity);

        DamagePopup popup = popupObj.GetComponent<DamagePopup>();
        if (popup != null)
        {
            // 参数对齐：现在是 6 个参数，最后一个是自身预制体引用
            popup.Setup(amount, isCrit, isTrueDamage, isArmorAbsorb, isPlayer, PopupPrefab);
        }
    }
}