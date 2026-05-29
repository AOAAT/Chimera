using System.Collections.Generic;
using UnityEngine;

// 带宽提供者接口，未来信号塔也实现它
public interface ICoverageProvider
{
    Vector3 GetPosition();
    float GetRadius();
}

public class ZoneCoverageManager : MonoBehaviour
{
    public static ZoneCoverageManager Instance;
    public System.Action OnProvidersChanged; // 🌟 新增事件

    private List<ICoverageProvider> providers = new List<ICoverageProvider>();
    public List<ICoverageProvider> GetAllProviders() => providers; // 🌟 暴露给渲染器

    private void Awake() => Instance = this;

    public void RegisterProvider(ICoverageProvider provider)
    {
        if (!providers.Contains(provider))
        {
            providers.Add(provider);
            OnProvidersChanged?.Invoke(); // 触发刷新
        }
    }

    public void UnregisterProvider(ICoverageProvider provider)
    {
        if (providers.Contains(provider))
        {
            providers.Remove(provider);
            OnProvidersChanged?.Invoke();
        }
    }

    /// <summary>
    /// 核心判定：目标点是否落在任何一个带宽提供者的范围内？
    /// </summary>
    public bool IsPointInCoverage(Vector3 worldPos)
    {
        if (providers.Count == 0)
        {
            // Debug.LogWarning("[中枢] 当前没有任何带宽提供者！");
            return false;
        }

        foreach (var provider in providers)
        {
            float dist = Vector3.Distance(worldPos, provider.GetPosition());
            float radius = provider.GetRadius();

            // 仅在调试时开启此行，防止刷屏
            // Debug.Log($"[检查] 距离源:{dist:F1} | 允许半径:{radius:F1}");

            if (dist <= radius) return true;
        }
        return false;
    }
}