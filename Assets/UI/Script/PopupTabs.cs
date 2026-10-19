using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 弹窗内面板切换器（挂在弹窗预制体根节点）。
/// 右侧竖排三个按钮：Btn_1=向上切换、Btn_2=向下切换、Btn_TelePhone=电话。Btn_3 自动隐藏。
/// 内容页两种模式：
///   A. 拖入内容页 GameObject → 切换显示对应页、隐藏其他页
///   B. 不拖内容页，只填 tabTexts → 把当前页文字写进弹窗正文(ContentText)
/// 到头就停（不循环）：第1页再向上无反应，最后一页再向下无反应。
/// </summary>
public class PopupTabs : MonoBehaviour
{
    [Header("按钮（留空自动查找 Btn_1 / Btn_2 / Btn_TelePhone）")]
    [Tooltip("向上切换面板")]
    public Button pageUpButton;
    [Tooltip("向下切换面板")]
    public Button pageDownButton;
    [Tooltip("电话按钮，点击触发审批电话逻辑")]
    public Button phoneButton;
    [Tooltip("要隐藏的按钮(Btn_3)；留空自动查找")]
    public Button unusedButton;

    [Header("内容页（模式A：拖入页面 GameObject）")]
    public List<GameObject> tabPages = new List<GameObject>();

    [Header("每页文案（模式B：没拖内容页时，作为弹窗正文）")]
    [TextArea(3, 8)] public List<string> tabTexts = new List<string>();

    [Header("右侧竖排布局")]
    [Tooltip("勾选后每次弹窗打开都按下方参数摆放右侧三个按钮；取消则用预制体里手摆的位置")]
    public bool autoPlaceButtons = true;
    [Tooltip("按钮距弹窗右边缘的水平间隙(像素)：越大越不靠边")]
    public float rightPadding = 150f;
    [Tooltip("相邻两个按钮的垂直间隔(像素)：两个按钮中心相距这么远")]
    public float buttonSpacing = 170f;
    [Tooltip("整组按钮相对弹窗垂直中心的偏移(像素)：正=整组上移，0=居中")]
    public float verticalOffset = 0f;
    [Tooltip("三个按钮的尺寸(像素)。任一维<=0 则该维用预制体原尺寸")]
    public Vector2 buttonSize = new Vector2(0f, 0f);

    [System.Serializable]
    public class ButtonStyle
    {
        [Tooltip("替换按钮图片（按钮自身 Image 的 Sprite）；留空=不改")]
        public Sprite sprite;
        [Range(0f, 1f)]
        [Tooltip("按钮透明度：0=全透明，1=不透明")]
        public float alpha = 1f;
    }

    [Header("三个按钮图片/透明度（顺序：Btn_1向上 / Btn_2向下 / 电话）")]
    [Tooltip("每项对应一个按钮：替换图片 + 透明度。可少于 3 项，缺的按钮保持原图/不透明")]
    public List<ButtonStyle> buttonStyles = new List<ButtonStyle>();

    [Header("三个按钮文字（顺序：Btn_1向上 / Btn_2向下 / 电话）")]
    [Tooltip("勾选后：清空三个按钮上原有的文字（做纯图标按钮）。某个按钮的文字槽位里填了字，则优先用槽位里的字")]
    public bool hideButtonTexts = true;
    [Tooltip("单独覆盖按钮文字（顺序：Btn_1向上 / Btn_2向下 / 电话）：填字=改成这个字；留空或没有该项=按上面的开关处理")]
    public List<string> buttonTexts = new List<string>();

    private int _current;

    void Awake()
    {
        AutoFindButtons();

        // Btn_1 向上、Btn_2 向下（每次点击按当前页计算，到头自动夹住，不循环）
        if (pageUpButton != null)
            pageUpButton.onClick.AddListener(() => SelectTab(_current - 1));
        if (pageDownButton != null)
            pageDownButton.onClick.AddListener(() => SelectTab(_current + 1));

        // 电话按钮：点击转发给 TutorialManager 的电话审批逻辑（教程推进/自由审批都走这里）
        if (phoneButton != null)
            phoneButton.onClick.AddListener(OnPhoneButtonClicked);
    }

    void Start()
    {
        if (unusedButton != null) unusedButton.gameObject.SetActive(false);

        ApplyButtonSizes();
        ApplyButtonPlacements();
        ApplyButtonStyles();
        ApplyButtonTexts();

        // Start 而不是 Awake：RoomInteract.OpenPopup 在 Instantiate 后会 SetText 覆盖正文，
        // 延迟到 Start 再选中第一页，保证初始内容不被覆盖
        SelectTab(0);
    }

    private void OnPhoneButtonClicked()
    {
        Debug.Log("[PopupTabs] 弹窗内电话按钮被点击 → TutorialManager.OnPhoneClicked");
        if (TutorialManager.Instance != null)
            TutorialManager.Instance.OnPhoneClicked();
    }

    /// <summary>页数：优先用内容页数量，否则用文案数量</summary>
    private int PageCount
    {
        get { return tabPages.Count > 0 ? tabPages.Count : tabTexts.Count; }
    }

    /// <summary>按 buttonSize 统一设置三个按钮尺寸（<=0 的维度不改，保持预制体原尺寸）</summary>
    private void ApplyButtonSizes()
    {
        if (buttonSize.x <= 0f && buttonSize.y <= 0f) return;
        for (int i = 0; i < 3; i++)
        {
            Button btn = GetStyleButton(i);
            if (btn == null) continue;
            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt == null) continue;

            Vector2 s = rt.sizeDelta;
            if (buttonSize.x > 0f) s.x = buttonSize.x;
            if (buttonSize.y > 0f) s.y = buttonSize.y;
            rt.sizeDelta = s;
        }
    }

    /// <summary>摆到右侧竖排：上=Btn_1(向上)，中=Btn_2(向下)，下=电话。全部锚定弹窗右缘。</summary>
    private void ApplyButtonPlacements()
    {
        if (!autoPlaceButtons) return;

        PlaceRight(pageUpButton, verticalOffset + buttonSpacing);   // 上
        PlaceRight(pageDownButton, verticalOffset);                 // 中
        PlaceRight(phoneButton, verticalOffset - buttonSpacing);    // 下
    }

    private void PlaceRight(Button btn, float y)
    {
        if (btn == null) return;
        RectTransform rt = btn.GetComponent<RectTransform>();
        if (rt == null) return;

        float halfW = rt.rect.width * 0.5f; // 按钮半宽，保证外缘距弹窗右边恰好 rightPadding
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f); // 弹窗右缘
        rt.anchoredPosition = new Vector2(-(rightPadding + halfW), y);
    }

    /// <summary>按钮顺序：0=向上 1=向下 2=电话（与图片/透明度槽位顺序一致）</summary>
    private Button GetStyleButton(int i)
    {
        if (i == 0) return pageUpButton;
        if (i == 1) return pageDownButton;
        return phoneButton;
    }

    private float StyleAlpha(int i)
    {
        return (i >= 0 && i < buttonStyles.Count) ? buttonStyles[i].alpha : 1f;
    }

    /// <summary>按 buttonStyles 槽位替换三个按钮的图片、设置透明度</summary>
    private void ApplyButtonStyles()
    {
        for (int i = 0; i < 3; i++)
        {
            Button btn = GetStyleButton(i);
            if (btn == null) continue;

            Image img = btn.targetGraphic as Image;
            if (img == null) img = btn.GetComponentInChildren<Image>();
            if (img == null) continue;

            // 图片：槽位有拖才替换，留空保持原图
            if (i < buttonStyles.Count && buttonStyles[i].sprite != null)
                img.sprite = buttonStyles[i].sprite;

            Color c = img.color;
            c.a = StyleAlpha(i);
            img.color = c;
        }
    }

    /// <summary>按钮文字：槽位填了字就用槽位的字；否则 hideButtonTexts 勾选时清空原有文字，不勾选则保持原文字</summary>
    private void ApplyButtonTexts()
    {
        for (int i = 0; i < 3; i++)
        {
            Button btn = GetStyleButton(i);
            if (btn == null) continue;

            string overrideText = (i < buttonTexts.Count) ? buttonTexts[i] : null;

            if (!string.IsNullOrEmpty(overrideText))
                SetButtonLabel(btn, overrideText);   // 槽位填了字 → 用槽位的字
            else if (hideButtonTexts)
                SetButtonLabel(btn, "");             // 勾选了清空 → 去掉按钮原有的字
            // 否则保持预制体原文字
        }
    }

    /// <summary>把按钮上的文字(兼容 TMP / Legacy Text)设为指定内容</summary>
    private void SetButtonLabel(Button btn, string label)
    {
        if (btn == null) return;

        TMP_Text tmp = btn.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) { tmp.text = label; return; }

        Text t = btn.GetComponentInChildren<Text>(true);
        if (t != null) t.text = label;
    }

    private void AutoFindButtons()
    {
        if (pageUpButton == null)
        {
            Transform t = FindDeepChild(transform, "Btn_1");
            if (t != null) pageUpButton = t.GetComponent<Button>();
        }
        if (pageDownButton == null)
        {
            Transform t = FindDeepChild(transform, "Btn_2");
            if (t != null) pageDownButton = t.GetComponent<Button>();
        }
        if (phoneButton == null)
        {
            Transform t = FindDeepChild(transform, "Btn_TelePhone");
            if (t != null) phoneButton = t.GetComponent<Button>();
        }
        if (unusedButton == null)
        {
            Transform t = FindDeepChild(transform, "Btn_3");
            if (t != null) unusedButton = t.GetComponent<Button>();
        }
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

    /// <summary>切换到指定页（0-based，自动夹在有效范围内；到头就停不循环）</summary>
    public void SelectTab(int index)
    {
        _current = Mathf.Clamp(index, 0, Mathf.Max(0, PageCount - 1));

        // 模式A：切换内容页显隐
        if (tabPages.Count > 0)
        {
            for (int i = 0; i < tabPages.Count; i++)
            {
                if (tabPages[i] != null) tabPages[i].SetActive(i == _current);
            }
        }
        // 模式B：切换弹窗正文文字
        else if (_current < tabTexts.Count)
        {
            PopupPanel panel = GetComponent<PopupPanel>();
            // 如果是表单弹窗（有 documentFields），正文由逐字段数据驱动，这里不能再塞 tabTexts 覆盖掉
            if (panel != null && panel.contentLegacy != null)
            {
                bool isForm = panel.documentFields != null && panel.documentFields.Count > 0;
                if (!isForm)
                    panel.contentLegacy.text = tabTexts[_current];
            }
        }

        Debug.Log($"[PopupTabs] 切换到第 {_current + 1}/{Mathf.Max(1, PageCount)} 页");
    }
}
