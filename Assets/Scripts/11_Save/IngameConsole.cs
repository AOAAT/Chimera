using UnityEngine;
using System.Collections.Generic;

public class IngameConsole : MonoBehaviour
{
    private string logText = "";
    private Queue<string> logQueue = new Queue<string>();

    void Awake()
    {
        // 🌟 核心修复 1：保活机制。确保它存活于所有场景和清理逻辑中
        DontDestroyOnLoad(this.gameObject);
    }

    void OnEnable() { Application.logMessageReceived += HandleLog; }
    void OnDisable() { Application.logMessageReceived -= HandleLog; }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        // 容量调大到 40，确保开局的追踪日志不会被顶掉
        if (logQueue.Count >= 40) logQueue.Dequeue();

        string color = type == LogType.Error || type == LogType.Exception ? "red" : "white";

        string traceInfo = "";
        if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace))
        {
            string[] lines = stackTrace.Split('\n');
            if (lines.Length > 0)
            {
                traceInfo = $"\n<size=18>  ↳ {lines[0]}</size>";
            }
        }

        logQueue.Enqueue($"<color={color}>{logString}{traceInfo}</color>");
        logText = string.Join("\n", logQueue);
    }

    void OnGUI()
    {
        // 🌟 核心修复 2：抛弃强行 Scale，改用根据屏幕高度动态算字体大小
        int fontSize = Mathf.Clamp(Screen.height / 40, 12, 36);
        GUI.skin.label.fontSize = fontSize;
        GUI.skin.box.fontSize = fontSize;

        float width = Screen.width * 0.45f;  // 占据左侧 45% 的屏幕宽度
        float height = Screen.height * 0.8f; // 占据 80% 的高度

        // 绘制半透明黑底背景
        GUI.Box(new Rect(10, 10, width, height), "");
        // 绘制文字
        GUI.Label(new Rect(20, 20, width - 20, height - 20), logText);
    }
}