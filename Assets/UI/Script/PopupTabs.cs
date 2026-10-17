using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 弹窗内 Tab 分页切换器（挂在弹窗预制体根节点）。
/// 两种模式（任选其一）：
///   A. 拖入内容页 GameObject → 点击 Btn_n 显示第 n 页、隐藏其他页
///   B. 不拖内容页，只填 tabTexts → 点击 Btn_n 把第 n 段文字写进弹窗正文(ContentText)
/// 按钮留空会自动查找子物体 Btn_1/Btn_2/Btn_3。
/// 选中按钮会变色+放大，方便看出当前在第几页。
/// </summary>
public class PopupTabs : MonoBehaviour
{
    [Header("Tab 按钮（留空自动查找 Btn_1/Btn_2/Btn_3）")]
    public List<Button> tabButtons = new List<Button>();

    [Header("模式A：内容页 GameObject（可选；拖入则点击切换显隐）")]
    public List<GameObject> tabPages = new List<GameObject>();

    [Header("模式B：每页文案（可选；没拖内容页时，点击把这段文字写进弹窗正文）")]
    [TextArea(3, 8)] public List<string> tabTexts = new List<string>();

    [Header("选中反馈（可改）")]
    [Tooltip("选中按钮颜色")]
    public Color selectedColor = new Color(1f, 0.8f, 0.2f, 1f);
    [Tooltip("未选中按钮颜色")]
    public Color normalColor = Color.white;
    [Tooltip("选中按钮放大倍数")]
    [Range(1f, 1.5f)] public float selectedScale = 1.15f;

    private int _current;

    void Awake()
    {
        AutoFindButtons();

        for (int i = 0; i < tabButtons.Count; i++)
        {
            int index = i; // 闭包捕获
            if (tabButtons[i] != null)
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
        }
    }

    void Start()
    {
        // Start 而不是 Awake：RoomInteract.OpenPopup 在 Instantiate 后会 SetText 覆盖正文，
        // 延迟到 Start 再选中第一页，保证初始内容不被覆盖
        SelectTab(0);
    }

    private void AutoFindButtons()
    {
        if (tabButtons.Count > 0) return;

        for (int i = 1; i <= 3; i++)
        {
            Transform t = FindDeepChild(transform, "Btn_" + i);
            if (t != null)
            {
                Button b = t.GetComponent<Button>();
                if (b != null) tabButtons.Add(b);
            }
        }
        if (tabButtons.Count == 0)
            Debug.LogWarning("[PopupTabs] 没找到 Btn_1/2/3，请在 Inspector 手动拖入 tabButtons");
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result = FindDeepChild(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }

    /// <summary>切换到指定页（0-based）。Btn_1→0, Btn_2→1, Btn_3→2</summary>
    public void SelectTab(int index)
    {
        _current = index;

        // 模式A：切换内容页显隐
        if (tabPages.Count > 0)
        {
            for (int i = 0; i < tabPages.Count; i++)
            {
                if (tabPages[i] != null) tabPages[i].SetActive(i == index);
            }
        }
        // 模式B：切换弹窗正文文字
        else if (index < tabTexts.Count)
        {
            PopupPanel panel = GetComponent<PopupPanel>();
            if (panel != null && panel.contentText != null)
            {
                panel.contentText.text = tabTexts[index];
            }
        }

        ApplyTabVisuals();
        Debug.Log($"[PopupTabs] 切换到第 {index + 1} 页");
    }

    /// <summary>选中按钮变色+放大，未选中恢复</summary>
    private void ApplyTabVisuals()
    {
        for (int i = 0; i < tabButtons.Count; i++)
        {
            Button btn = tabButtons[i];
            if (btn == null) continue;

            bool selected = (i == _current);

            Graphic g = btn.targetGraphic != null ? btn.targetGraphic : btn.GetComponentInChildren<Graphic>();
            if (g != null) g.color = selected ? selectedColor : normalColor;

            btn.transform.localScale = selected
                ? Vector3.one * selectedScale
                : Vector3.one;
        }
    }
}
