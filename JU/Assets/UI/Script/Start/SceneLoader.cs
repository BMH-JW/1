using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement; // 场景管理必须引用

public class SceneLoader : MonoBehaviour
{
    // 公开方法，给按钮点击事件调用
    public void LoadRoomScene()
    {
        // 场景名字必须和你的场景文件名【完全一模一样，大小写都要匹配】
        SceneManager.LoadScene("RoomScene");
    }
}