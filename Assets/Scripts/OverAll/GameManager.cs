using UnityEngine;

public class GameManager : MonoBehaviour
{
    void Update()
    {
        // 每一帧监听：如果玩家按下了 ESC 键
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            QuitGame();
        }
    }

    // 封装一个退出游戏的方法，方便以后给 UI 上的“退出游戏”按钮调用
    public void QuitGame()
    {
        Debug.Log("执行退出协议，关闭系统...");

        // 这段 #if 指令极其好用：它会判断你当前的环境
#if UNITY_EDITOR
        // 如果你在 Unity 编辑器里测试，它会让上面的播放按钮自动停止
        UnityEditor.EditorApplication.isPlaying = false;
#else
        // 如果你是在打包后的 EXE 里运行，它会直接关闭程序
        Application.Quit();
#endif
    }
}