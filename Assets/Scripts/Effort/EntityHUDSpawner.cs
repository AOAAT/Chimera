// --- START OF FILE EntityHUDSpawner.cs ---
using UnityEngine;

// 这个脚本挂在机甲预制体和怪物预制体上，自动生成头顶血条
[RequireComponent(typeof(DamageReceiver))]
[RequireComponent(typeof(BuffManager))]
public class EntityHUDSpawner : MonoBehaviour
{
    [Tooltip("拖入做好的 World Space 实体 HUD 预制体")]
    public GameObject HUDPrefab;
    [Tooltip("头顶偏移量")]
    public Vector3 Offset = new Vector3(0, 1.5f, 0);

    private void Start()
    {
        if (HUDPrefab == null) return;

        GameObject hudObj = Instantiate(HUDPrefab, this.transform);
        hudObj.transform.localPosition = Offset;

        EntityHUD hudScript = hudObj.GetComponent<EntityHUD>();
        if (hudScript != null)
        {
            hudScript.Initialize(GetComponent<DamageReceiver>(), GetComponent<BuffManager>());
        }
    }
}