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

    [Header("电话按钮（可选；弹窗内的 Btn_TelePhone，点击触发审批电话逻辑）")]
    [Tooltip("留空则自动查找子物体 Btn_TelePhone")]
    public Button phoneButton;

    [Header("电话按钮自动摆位")]
    [Tooltip("勾选后每次弹窗打开都把电话按钮摆到右下角（按下方偏移）；想自己在编辑器里摆就取消勾选")]
    public bool autoPlacePhone = true;
    [Tooltip("电话按钮中心相对弹窗右下角的偏移(像素)。X负=向左，Y正=向上。默认(-83,50)=贴右下角留20px内边距")]
    public Vector2 phoneCornerOffset = new Vector2(-83f, 50f);

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
        AutoFindPhoneButton();

        for (int i = 0; i < tabButtons.Count; i++)
        {
            int index = i; // 闭包捕获
            if (tabButtons[i] != null)
                tabButtons[i].onClick.AddListener(() => SelectTab(index));
        }

        // 电话按钮：点击转发给 TutorialManager 的电话审批逻辑（教程推进/自由审批都走这里）
        if (phoneButton != null)
        {
            phoneButton.onClick.AddListener(OnPhoneButtonClicked);
        }
    }

    private void AutoFindPhoneButton()
    {
        if (phoneButton != null) return;
        Transform t = FindDeepChild(transform, "Btn_TelePhone");
        if (t != null) phoneButton = t.GetComponent<Button>();
    }

    private void OnPhoneButtonClicked()
    {
        Debug.Log("[PopupTabs] 弹窗内电话按钮被点击 → TutorialManager.OnPhoneClicked");
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnPhoneClicked();
    }

    void Start()
    {
        ApplyPhonePlacement();

        // Start 而不是 Awake：RoomInteract.OpenPopup 在 Instantiate 后会 SetText 覆盖正文，
        // 延迟到 Start 再选中第一页，保证初始内容不被覆盖
        SelectTab(0);
    }

    /// <summary>把电话按钮摆到弹窗右下角（锚点=右下角，位置由 phoneCornerOffset 控制）</summary>
    private void ApplyPhonePlacement()
    {
        if (!autoPlacePhone || phoneButton == null) return;
        RectTransform rt = phoneButton.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = new Vector2(1f, 0f); // 右下角
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = phoneCornerOffset;
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
