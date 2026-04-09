// --- START OF FILE GlobalCPManager.cs ---
using System;
using UnityEngine;

public class GlobalCPManager : MonoBehaviour
{
    public static GlobalCPManager Instance;

    [Header("=== CP (指挥点/魔力) 设定 ===")]
    public float MaxCP = 20f;         // 默认上限
    public float CurrentCP = 20f;     // 当前剩余
    public float BaseRegenRate = 1f;  // 每秒自然回复 1 点

    // 预留给组件(遗物)的动态修正值
    public float BonusMaxCP = 0f;
    public float BonusRegenRate = 0f;

    // UI 监听事件
    public event Action OnCPChanged;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        CurrentCP = MaxCP;
        OnCPChanged?.Invoke();
    }

    private void Update()
    {
        // 只有在战斗中才自然回复 CP
        if (CombatDirector.Instance != null && CombatDirector.Instance.IsCombatActive)
        {
            float totalRegen = BaseRegenRate + BonusRegenRate;
            if (totalRegen > 0 && CurrentCP < GetActualMaxCP())
            {
                CurrentCP += totalRegen * Time.deltaTime;
                CurrentCP = Mathf.Clamp(CurrentCP, 0f, GetActualMaxCP());
                OnCPChanged?.Invoke(); // 通知 UI 刷新
            }
        }
    }

    public float GetActualMaxCP()
    {
        return Mathf.Max(0, MaxCP + BonusMaxCP);
    }

    // ==========================================
    // 核心交互接口
    // ==========================================

    // 检查够不够扣？
    public bool HasEnoughCP(float amount)
    {
        return CurrentCP >= amount;
    }

    // 扣除/增加 CP (正数增加，负数扣除)
    public bool ModifyCP(float amount)
    {
        if (amount < 0 && !HasEnoughCP(Mathf.Abs(amount)))
        {
            return false; // 不够扣！
        }

        CurrentCP = Mathf.Clamp(CurrentCP + amount, 0f, GetActualMaxCP());
        OnCPChanged?.Invoke();
        return true;
    }
}