using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// 剧情场景：依次展示图片1~4，每页下方打字机弹出文案。
/// 两阶段点击：打字中点击一下 → 全文立即出现；再点击一下 → 翻到下一张/跳转场景。
/// 挂在 PlotScene 的 Canvas 上即可（自动查找子物体 1/2/3/4）。
/// </summary>
public class PlotController : MonoBehaviour
{
    [Header("剧情图片（按顺序1~4；留空自动查找Canvas下名字为1/2/3/4的物体）")]
    public GameObject[] pages;

    [Header("每页文案（按顺序对应图片1~4；可以少于页数，多余页会显示空文案）")]
    [TextArea(2, 6)] public List<string> pageTexts = new List<string>();

    [Header("文案文字 —— 外观")]
    [Tooltip("打字机速度：每个字间隔秒数，值越小越快")]
    public float typeInterval = 0.04f;
    [Tooltip("字体，留空用系统自带 LegacyRuntime.ttf")]
    public Font textFont;
    [Tooltip("字号")]
    public int textFontSize = 40;
    [Tooltip("颜色")]
    public Color textColor = Color.white;
    [Tooltip("对齐方式")]
    public TextAnchor textAnchor = TextAnchor.MiddleCenter;

    [Header("文案文字 —— 位置/尺寸")]
    [Tooltip("相对屏幕底部的距离(像素)；大=文字靠上，小=文字靠下")]
    public float textBottomOffset = 160f;
    [Tooltip("文字宽度(像素)；0 或负数 = 撑满屏幕左右各留 60px")]
    public float textWidth = 900f;
    [Tooltip("文字区域高度(像素)")]
    public float textHeight = 180f;

    [Header("文案文字 —— 背景泡泡(可选)")]
    [Tooltip("勾上就给文案后面加一个半透明圆角白底气泡")]
    public bool useTextBubble = true;
    [Tooltip("泡泡 Sprite；留空则代码动态生成一个 1x1 白块用 Sliced")]
    public Sprite textBubbleSprite;
    [Tooltip("泡泡颜色(含alpha)")]
    public Color textBubbleColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("看完最后一张后跳转的场景名")]
    public string nextScene = "RoomScene";

    [Header("左上角提示小字")]
    [Tooltip("提示文字内容；留空就不渲染")]
    public string hintText = "点击屏幕即可跳转";
    [Tooltip("字号")]
    public int hintFontSize = 22;
    [Tooltip("颜色(含alpha)")]
    public Color hintColor = new Color(1f, 1f, 1f, 0.85f);
    [Tooltip("相对屏幕左上角的偏移(像素)：X正=向右，Y负=向下")]
    public Vector2 hintTopLeftOffset = new Vector2(40f, -40f);

    private int _current;
    private Text _text;
    private Image _bubbleImg;
    private RectTransform _textRT;
    private RectTransform _bubbleRT;
    private Coroutine _typingRoutine;
    private bool _typingDone;
    private Text _hintText;
    private RectTransform _hintRT;

    void Awake()
    {
        if (pages == null || pages.Length == 0) AutoFindPages();
        BuildTextUI();
        BuildHintText();
    }

    void Start()
    {
        ShowPage(0);
    }

    void Update()
    {
        if (!Input.GetMouseButtonDown(0)) return;
        HandleClick();
    }

    /// <summary>两阶段点击：打字中 → 立刻全文出现；打字完 → 翻下一页/跳转</summary>
    private void HandleClick()
    {
        if (_typingRoutine != null && !_typingDone)
        {
            // 阶段1：打断打字，立刻全文出现
            StopCoroutine(_typingRoutine);
            _typingRoutine = null;
            ApplyFullTextForCurrentPage();
            _typingDone = true;
            return;
        }

        // 阶段2：翻页
        NextPage();
    }

    private void AutoFindPages()
    {
        var list = new List<GameObject>();
        for (int i = 1; i <= 4; i++)
        {
            Transform t = transform.Find(i.ToString());
            if (t != null) list.Add(t.gameObject);
        }
        pages = list.ToArray();
    }

    private Font ResolveFont()
    {
        if (textFont != null) return textFont;
        Font f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
        return f;
    }

    /// <summary>运行时构建底部文案 UI（挂在 Canvas 下，独立于每页图片）</summary>
    private void BuildTextUI()
    {
        Font font = ResolveFont();

        // 背景泡泡（先建，保证在 Text 下方渲染）
        if (useTextBubble)
        {
            GameObject bg = new GameObject("TextBubble", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            _bubbleImg = bg.GetComponent<Image>();
            _bubbleImg.sprite = textBubbleSprite != null ? textBubbleSprite : MakeWhiteSprite();
            _bubbleImg.type = Image.Type.Sliced;
            _bubbleImg.color = textBubbleColor;
            _bubbleImg.raycastTarget = false;
            _bubbleRT = bg.GetComponent<RectTransform>();
        }

        // 文字
        GameObject t = new GameObject("StoryText", typeof(RectTransform));
        t.transform.SetParent(transform, false);
        _text = t.AddComponent<Text>();
        _text.font = font;
        _text.fontSize = textFontSize;
        _text.color = textColor;
        _text.alignment = textAnchor;
        _text.horizontalOverflow = HorizontalWrapMode.Wrap;
        _text.verticalOverflow = VerticalWrapMode.Overflow;
        _text.raycastTarget = false;
        _textRT = _text.rectTransform;

        ApplyTextLayout();
    }

    /// <summary>把文字 + 泡泡定位到屏幕下方，宽度撑满</summary>
    private void ApplyTextLayout()
    {
        RectTransform canvasRT = GetComponent<RectTransform>();
        Vector2 canvasSize = canvasRT != null ? canvasRT.rect.size : new Vector2(1920f, 1080f);

        float w = textWidth > 0f ? textWidth : Mathf.Max(200f, canvasSize.x - 120f);
        float h = Mathf.Max(40f, textHeight);

        // 文字：底部锚点，anchoredPosition.y = textBottomOffset（距底多高）
        _textRT.anchorMin = new Vector2(0.5f, 0f);
        _textRT.anchorMax = new Vector2(0.5f, 0f);
        _textRT.pivot = new Vector2(0.5f, 0f);
        _textRT.sizeDelta = new Vector2(w, h);
        _textRT.anchoredPosition = new Vector2(0f, textBottomOffset);

        // 泡泡比文字略宽略高一圈
        if (_bubbleRT != null)
        {
            const float padX = 40f;
            const float padY = 24f;
            _bubbleRT.anchorMin = new Vector2(0.5f, 0f);
            _bubbleRT.anchorMax = new Vector2(0.5f, 0f);
            _bubbleRT.pivot = new Vector2(0.5f, 0f);
            _bubbleRT.sizeDelta = new Vector2(w + padX * 2f, h + padY * 2f);
            _bubbleRT.anchoredPosition = new Vector2(0f, textBottomOffset - padY);
        }
    }

    /// <summary>从 0x00000001 白像素生成一个 Sprite，用于代码兜底泡泡底</summary>
    private static Sprite MakeWhiteSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        var cols = new Color32[16];
        for (int i = 0; i < 16; i++) cols[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }

    /// <summary>左上角的提示小字（"点击屏幕即可跳转" 等）</summary>
    private void BuildHintText()
    {
        if (string.IsNullOrEmpty(hintText)) return;

        Font font = textFont != null ? textFont : ResolveFont();

        GameObject t = new GameObject("HintText", typeof(RectTransform));
        t.transform.SetParent(transform, false);
        _hintText = t.AddComponent<Text>();
        _hintText.font = font;
        _hintText.fontSize = hintFontSize;
        _hintText.color = hintColor;
        _hintText.alignment = TextAnchor.MiddleLeft;
        _hintText.horizontalOverflow = HorizontalWrapMode.Overflow;
        _hintText.verticalOverflow = VerticalWrapMode.Overflow;
        _hintText.text = hintText;
        _hintText.raycastTarget = false;
        _hintRT = _hintText.rectTransform;

        ApplyHintLayout();
    }

    private void ApplyHintLayout()
    {
        if (_hintRT == null) return;

        // 左上角锚点，宽度自适应文字（用 sizeDelta 的 x 不限制）
        _hintRT.anchorMin = new Vector2(0f, 1f);
        _hintRT.anchorMax = new Vector2(0f, 1f);
        _hintRT.pivot = new Vector2(0f, 1f);
        _hintRT.sizeDelta = new Vector2(0f, hintFontSize * 1.4f);
        _hintRT.anchoredPosition = hintTopLeftOffset;
    }

    private void NextPage()
    {
        if (pages == null || pages.Length == 0) return;

        if (_current < pages.Length - 1) ShowPage(_current + 1);
        else SceneManager.LoadScene(nextScene);
    }

    private void ShowPage(int index)
    {
        for (int i = 0; i < pages.Length; i++)
            if (pages[i] != null) pages[i].SetActive(i == index);
        _current = index;

        // 停掉旧的打字协程，开始打新的
        if (_typingRoutine != null) StopCoroutine(_typingRoutine);
        _typingDone = false;

        string content = (pageTexts != null && index >= 0 && index < pageTexts.Count) ? pageTexts[index] : "";
        _typingRoutine = StartCoroutine(TypeTextRoutine(content));

        Debug.Log($"[PlotController] 展示剧情图片 {index + 1}/{pages.Length}");
    }

    private IEnumerator TypeTextRoutine(string content)
    {
        _text.text = "";
        if (string.IsNullOrEmpty(content))
        {
            _typingDone = true;
            yield break;
        }

        foreach (char c in content)
        {
            _text.text += c;
            yield return new WaitForSeconds(typeInterval);
        }
        _typingDone = true;
        _typingRoutine = null;
    }

    private void ApplyFullTextForCurrentPage()
    {
        string content = (pageTexts != null && _current >= 0 && _current < pageTexts.Count) ? pageTexts[_current] : "";
        _text.text = content ?? "";
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_textRT != null && _bubbleRT != null) ApplyTextLayout();
        if (_hintRT != null) ApplyHintLayout();
        if (_hintText != null)
        {
            if (!string.IsNullOrEmpty(hintText)) _hintText.text = hintText;
            _hintText.fontSize = hintFontSize;
            _hintText.color = hintColor;
        }
    }
#endif
}
