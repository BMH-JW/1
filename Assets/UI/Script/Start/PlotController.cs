using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 剧情场景：依次展示图片1~4，每次点击 Btn_Skip 翻到下一张；
/// 在第4张点击 Btn_Skip 后跳转到 RoomScene。
/// 挂在 PlotScene 的 Canvas 上即可（自动查找子物体 1/2/3/4 和 Btn_Skip）。
/// </summary>
public class PlotController : MonoBehaviour
{
    [Header("剧情图片（按顺序1~4；留空自动查找Canvas下名字为1/2/3/4的物体）")]
    public GameObject[] pages;

    [Header("跳过/下一页按钮（留空自动查找 Btn_Skip）")]
    public Button skipButton;

    [Header("看完4张后跳转的场景名")]
    public string nextScene = "RoomScene";

    private int _current;

    void Awake()
    {
        if (pages == null || pages.Length == 0) AutoFindPages();
        if (skipButton == null) AutoFindSkipButton();
    }

    void Start()
    {
        ShowPage(0);

        if (skipButton != null)
        {
            skipButton.onClick.RemoveListener(OnSkipClicked);
            skipButton.onClick.AddListener(OnSkipClicked);
        }
        else
        {
            Debug.LogError("[PlotController] 找不到 Btn_Skip，请在 Inspector 里手动指定 skipButton");
        }
    }

    private void AutoFindPages()
    {
        var list = new List<GameObject>();
        for (int i = 1; i <= 4; i++)
        {
            Transform t = transform.Find(i.ToString());
            if (t != null)
            {
                list.Add(t.gameObject);
            }
            else
            {
                Debug.LogError($"[PlotController] Canvas 下找不到名为 {i} 的图片物体");
            }
        }
        pages = list.ToArray();
    }

    private void AutoFindSkipButton()
    {
        GameObject btnGo = FindByName(transform, "Btn_Skip");
        if (btnGo != null)
        {
            skipButton = btnGo.GetComponent<Button>();
            if (skipButton == null) skipButton = btnGo.GetComponentInChildren<Button>();
        }
    }

    private static GameObject FindByName(Transform root, string name)
    {
        if (root.name == name) return root.gameObject;
        for (int i = 0; i < root.childCount; i++)
        {
            GameObject result = FindByName(root.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>Btn_Skip 点击入口：还有下一张就翻页，最后一张则进 RoomScene</summary>
    public void OnSkipClicked()
    {
        if (pages == null || pages.Length == 0) return;

        if (_current < pages.Length - 1)
        {
            ShowPage(_current + 1);
        }
        else
        {
            SceneManager.LoadScene(nextScene);
        }
    }

    private void ShowPage(int index)
    {
        for (int i = 0; i < pages.Length; i++)
        {
            if (pages[i] != null) pages[i].SetActive(i == index);
        }
        _current = index;
        Debug.Log($"[PlotController] 展示剧情图片 {index + 1}/{pages.Length}");
    }
}
