using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    /// <summary>开始游戏：先进入剧情场景 PlotScene（1~4 张剧情图）</summary>
    public void LoadPlotScene()
    {
        SceneManager.LoadScene("PlotScene");
    }

    /// <summary>直接进入房间场景（保留，剧情结束后由 PlotController 调用同名加载）</summary>
    public void LoadRoomScene()
    {
        SceneManager.LoadScene("RoomScene");
    }

    /// <summary>退出游戏：编辑器里停止运行，打包后退出程序</summary>
    public void QuitGame()
    {
        Debug.Log("[SceneLoader] 退出游戏");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
