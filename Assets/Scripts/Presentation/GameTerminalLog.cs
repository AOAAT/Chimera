using UnityEngine;
using TMPro;
using System.Collections.Generic;
using System.Text;

public class GameTerminalLog : MonoBehaviour
{
    public static GameTerminalLog Instance;

    [Header("=== UI 绑定 ===")]
    public TMP_Text LogContentText;
    public GameObject ConsolePanel;

    [Header("=== 状态控制 ===")]
    public bool IsFrozen = false; // 👈 核心控制：是否处于数据冻结状态

    public int MaxLines = 25;

    private List<string> logList = new List<string>();
    private bool isDirty = false;
    private StringBuilder sb = new StringBuilder();

    private void Awake()
    {
        Instance = this;
        Application.logMessageReceived += HandleLog;
    }

    private void OnDestroy() => Application.logMessageReceived -= HandleLog;

    // 👈 供外部调用的切换接口
    public void SetFreeze(bool freeze)
    {
        IsFrozen = freeze;
        Debug.Log(freeze ? "<color=red>【监控系统】</color> 战斗环境干扰中，日志记录已挂起。" : "<color=green>【监控系统】</color> 链路恢复，正在同步最新日志。");
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        // 🔒 性能锁 A：如果被冻结了，直接无视这行日志，不进行任何计算
        if (IsFrozen) return;

        string colorHex = "#FFFFFF";
        string prefix = "[SYS]";

        switch (type)
        {
            case LogType.Error: case LogType.Exception: colorHex = "#FF4444"; prefix = "[ERR]"; break;
            case LogType.Warning: colorHex = "#FFCC00"; prefix = "[WRN]"; break;
            case LogType.Log:
                if (logString.Contains("【")) colorHex = "#00FFFF";
                else colorHex = "#AAAAAA";
                break;
        }

        string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        logList.Add($"<{timestamp}> <color={colorHex}>{prefix} {logString}</color>");

        if (logList.Count > MaxLines) logList.RemoveAt(0);
        isDirty = true;
    }

    private void LateUpdate()
    {
        // 🔒 性能锁 B：如果被冻结了，不仅不计算文字，甚至不跑 UI 更新逻辑
        if (IsFrozen) return;

        if (isDirty && LogContentText != null)
        {
            sb.Clear();
            for (int i = 0; i < logList.Count; i++) sb.AppendLine(logList[i]);
            LogContentText.text = sb.ToString();
            isDirty = false;
        }

        if (Input.GetKeyDown(KeyCode.BackQuote))
            ConsolePanel.SetActive(!ConsolePanel.activeSelf);
    }
}