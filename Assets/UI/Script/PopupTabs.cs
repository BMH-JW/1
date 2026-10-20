using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 弹窗内面板切换器（挂在弹窗预制体根节点）。
/// 右侧竖排三个按钮：Btn_1=向上切换、Btn_2=向下切换、Btn_TelePhone=电话。Btn_3 自动隐藏。
/// 内容页两种模式：
///   A. 拖入内容页 GameObject → 切换显示对应页、隐藏其他页
///   B. 只填 tabTexts / tabBodies → 标题框写 tabTexts[页]，正文框写 tabBodies[页]
///      （tabBodies 里的 {RULES} 会自动换成今日规则公告）
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

    [Header("每页文案：写进弹窗**标题框**（页数 = 这个列表条数）")]
    [TextArea(1, 3)] public List<string> tabTexts = new List<string>();

    [Header("每页正文（可选）：与页数一一对应，写进弹窗正文框；{RULES} = 今日规则公告；空=该页不显示正文")]
    [TextArea(3, 8)] public List<string> tabBodies = new List<string>();

    [Tooltip("勾选后：切到非第 1 页时隐藏「文字框 ↔ 内容键」里的所有框；切回第 1 页按框内有没有文字恢复显示")]
    public bool switchFieldBoxes = true;

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

    // 第 1 页正文（新闻）由 GameSessionBridge 往「文字框 ↔ 内容键」里填，
    // 这里先把打开时的正文记下来，翻到别的页再翻回来时原样还原，避免被清空
    private string _page0Body = "";
    private bool _page0BodyCaptured;

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

    /// <summary>切换到指定页（0-based，自动夹在有效范围内；到头就停不循环）
    /// 每页：标题框写 tabTexts[页]，正文框写 tabBodies[页]（{RULES} 自动换成今日规则公告）；
    /// 非第 1 页时把「文字框 ↔ 内容键」里的其他框全部隐藏，回第 1 页再按框内文字恢复。</summary>
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

        PopupPanel panel = GetComponent<PopupPanel>();

        // 结算弹窗（GameOver）只是复用同一预制体，不翻页 ——
        // 它的标题/正文由 GameSessionBridge 决定，这里别覆盖、也别动文字框显隐
        if (panel != null && panel.popupSource == "GameOver")
        {
            Debug.Log("[PopupTabs] popupSource=GameOver → 跳过翻页，保持结算文字");
            return;
        }

        if (panel != null)
        {
            // 标题：按当前页写进标题框（顺便确保它是显示状态 ——
            // 打开时正文优先的弹窗可能先把标题框藏起来了）
            Text titleBox = panel.GetTitleLegacy();
            if (titleBox != null && _current < tabTexts.Count)
            {
                titleBox.text = tabTexts[_current];
                if (titleBox.gameObject != null && !string.IsNullOrEmpty(titleBox.text))
                    titleBox.gameObject.SetActive(true);
            }

            // 正文：按当前页写进正文框（没配该页正文就空着并隐藏）
            // 特例：第 1 页正文由 GameSessionBridge 填（新闻），tabBodies[0] 为空时
            //       保留打开时已有的正文，并记住它，翻回来时还原
            Text contentBox = panel.GetContentLegacy();
            if (contentBox != null)
            {
                if (!_page0BodyCaptured)
                {
                    _page0Body = contentBox.text ?? "";
                    _page0BodyCaptured = true;
                }

                bool hasPageBody = _current < tabBodies.Count && !string.IsNullOrEmpty(tabBodies[_current]);
                string body = hasPageBody ? ResolveBody(tabBodies[_current]) : (_current == 0 ? _page0Body : "");

                contentBox.text = body ?? "";
                if (contentBox.gameObject != null)
                    contentBox.gameObject.SetActive(!string.IsNullOrEmpty(contentBox.text));
            }

            // 「文字框 ↔ 内容键」里的其他框：非第 1 页隐藏，回第 1 页按框内有没有文字恢复
            // （标题框、正文框上面已单独处理，这里跳过）
            if (switchFieldBoxes && panel.documentFields != null)
            {
                foreach (var f in panel.documentFields)
                {
                    if (f == null || f.slotGO == null) continue;
                    if (f.fieldName == "content" || f.fieldName == "title") continue;
                    f.slotGO.SetActive(_current == 0 && f.text != null && !string.IsNullOrEmpty(f.text.text));
                }
            }
        }

        Debug.Log($"[PopupTabs] 切换到第 {_current + 1}/{Mathf.Max(1, PageCount)} 页");
    }

    /// <summary>把正文里的 {RULES} 换成今日规则公告（其他内容原样返回）</summary>
    private static string ResolveBody(string body)
    {
        if (string.IsNullOrEmpty(body)) return body;
        if (body.IndexOf("{RULES}", System.StringComparison.Ordinal) < 0) return body;

        var bridge = GameSessionBridge.Instance;
        if (bridge != null && bridge.Current == null) bridge.EnsureSessionReady();
        string rules = (bridge != null && bridge.Current != null)
            ? bridge.Current.GetRuleAnnouncement()
            : "（规则暂无）";
        return body.Replace("{RULES}", rules ?? "");
    }
}
