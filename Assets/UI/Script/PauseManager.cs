using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// ESC 暂停管理器（自包含，挂在场景任意物体上）。
/// 按 ESC：变暗遮罩 + 中央暂停页图片 + 底部两个按钮 + 时间暂停；再按 ESC：恢复。
/// 两个按钮：退出游戏 / 回到主菜单。
/// </summary>
public class PauseManager : MonoBehaviour
{
    [Header("暂停页面图片")]
    [Tooltip("暂停时中央显示的图片（Sprite），不受变暗影响")]
    public Sprite pausePageSprite;

    [Header("外观（可改）")]
    [Tooltip("暂停页宽高(像素)")]
    public Vector2 pausePageSize = new Vector2(800f, 600f);
    [Tooltip("画面变暗的程度 0~1")]
    [Range(0f, 1f)] public float dimAlpha = 0.7f;
    [Tooltip("是否暂停游戏时间（Time.timeScale=0）")]
    public bool pauseTimeScale = true;

    [Header("底部按钮 —— 文案/跳转")]
    [Tooltip("退出按钮文案")]
    public string exitBtnText = "退出游戏";
    [Tooltip("回到主界面按钮文案")]
    public string backToMenuBtnText = "回到主界面";
    [Tooltip("暂停里点'回到主界面'要 Load 的场景名；二次进来直接进游戏界面(RoomScene)，跳过 PlotScene 剧情")]
    public string resumeSceneName = "RoomScene";

    [Header("底部按钮 —— 字体")]
    [Tooltip("按钮字体；留空用系统 LegacyRuntime.ttf")]
    public Font btnFont;
    [Tooltip("按钮字号")]
    public int btnFontSize = 28;

    [Header("🔊 音效（直接拖 .mp3/.wav）")]
    [Tooltip("按 ESC 弹出暂停界面时播")]
    public AudioClip pauseSfx;
    [Tooltip("再按 ESC / 点恢复时播")]
    public AudioClip resumeSfx;
    [Tooltip("暂停面板上按钮（回到主界面、退出游戏）的点击音效；留空用 AudioManager.defaultClickSfx")]
    public AudioClip pauseBtnClickSfx;
    [Tooltip("按钮文字颜色")]
    public Color btnTextColor = Color.white;

    [Header("中间按钮 —— 位置/尺寸")]
    [Tooltip("按钮尺寸(宽×高 像素)")]
    public Vector2 btnSize = new Vector2(260f, 70f);
    [Tooltip("两个按钮之间的垂直间距(像素)")]
    public float btnGap = 40f;
    [Tooltip("按钮组相对暂停页中心的垂直偏移(像素)：负=整体下移，正=上移；0=绝对居中")]
    public float btnCenterOffset = 0f;

    [Header("底部按钮 —— 外观")]
    [Tooltip("按钮背景 Sprite；留空代码生成白块用 Sliced")]
    public Sprite btnBackgroundSprite;
    [Tooltip("按钮背景颜色(含alpha)")]
    public Color btnBackgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.9f);

    private GameObject _pauseRoot;
    private Image _pausePageImg;
    private bool _paused;

#if UNITY_EDITOR
    void OnValidate()
    {
        // 暂停界面开着时，在 Inspector 改 pausePageSize / dimAlpha 立刻重摆
        if (_pausePageImg != null)
        {
            _pausePageImg.rectTransform.sizeDelta = pausePageSize;
        }
        if (_pauseRoot != null)
        {
            Image dim = _pauseRoot.transform.Find("Dim")?.GetComponent<Image>();
            if (dim != null) dim.color = new Color(0f, 0f, 0f, dimAlpha);
        }
    }
#endif

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        if (_paused) Resume();
        else Pause();
    }

    private void Pause()
    {
        _paused = true;
        if (pauseTimeScale) Time.timeScale = 0f;
        BuildPauseUI();
        Debug.Log("[PauseManager] 游戏暂停");

        // 🔊 暂停音效 —— 优先专属 pauseSfx，没配就 fallback 全局默认
        if (AudioManager.Instance != null)
        {
            var clip = pauseSfx != null ? pauseSfx : AudioManager.Instance.defaultClickSfx;
            if (clip != null) AudioManager.Instance.PlaySFX(clip);
        }
    }

    private void Resume()
    {
        _paused = false;
        if (pauseTimeScale) Time.timeScale = 1f;
        if (_pauseRoot != null)
        {
            Destroy(_pauseRoot);
            _pauseRoot = null;
            _pausePageImg = null;
        }
        Debug.Log("[PauseManager] 游戏恢复");

        // 🔊 恢复音效 —— 优先专属 resumeSfx，没配就 fallback 全局默认
        if (AudioManager.Instance != null)
        {
            var clip = resumeSfx != null ? resumeSfx : AudioManager.Instance.defaultClickSfx;
            if (clip != null) AudioManager.Instance.PlaySFX(clip);
        }
    }

    private void BuildPauseUI()
    {
        if (_pauseRoot != null) Destroy(_pauseRoot);

        // 独立 Canvas，ScreenSpaceOverlay + 最高排序，保证压在所有游戏UI之上
        _pauseRoot = new GameObject("PauseRoot");
        Canvas canvas = _pauseRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        _pauseRoot.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = _pauseRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 1. 变暗遮罩（全屏黑色半透明，先创建 = 渲染在底层）
        GameObject dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(_pauseRoot.transform, false);
        Image dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, dimAlpha);
        RectTransform dimRT = dimImg.rectTransform;
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        // 2. 暂停页面图片（后创建 = 渲染在遮罩之上，不受变暗影响）
        GameObject pageGo = new GameObject("PausePage");
        pageGo.transform.SetParent(_pauseRoot.transform, false);
        _pausePageImg = pageGo.AddComponent<Image>();
        _pausePageImg.sprite = pausePageSprite;
        _pausePageImg.preserveAspect = true;
        _pausePageImg.raycastTarget = false; // 图片本身不挡点击，点击由遮罩挡住
        RectTransform pageRT = _pausePageImg.rectTransform;
        pageRT.anchorMin = new Vector2(0.5f, 0.5f);
        pageRT.anchorMax = new Vector2(0.5f, 0.5f);
        pageRT.pivot = new Vector2(0.5f, 0.5f);
        pageRT.anchoredPosition = Vector2.zero;
        pageRT.sizeDelta = pausePageSize;

        if (pausePageSprite == null)
            Debug.LogWarning("[PauseManager] pausePageSprite 未设置，暂停页将是空白");

        BuildBottomButtons(pageRT);
    }

    // ================================================================
    // 中间两个按钮：回到主界面 / 退出游戏 —— 垂直堆叠，页面居中
    // ================================================================
    private void BuildBottomButtons(RectTransform parentRT)
    {
        Font font = btnFont != null ? btnFont :
            (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
             Resources.GetBuiltinResource<Font>("Arial.ttf"));
        Sprite bg = btnBackgroundSprite != null ? btnBackgroundSprite : MakeWhiteSprite();

        // 布局：两个按钮垂直堆叠，整体居中
        // 上按钮 y =  (btnSize.y + btnGap) * 0.5 + btnCenterOffset
        // 下按钮 y = -(btnSize.y + btnGap) * 0.5 + btnCenterOffset
        float halfStack = (btnSize.y + btnGap) * 0.5f;
        float topY = halfStack + btnCenterOffset;
        float bottomY = -halfStack + btnCenterOffset;

        BuildOneButton(
            parentRT: parentRT,
            anchoredPos: new Vector2(0f, topY),
            text: backToMenuBtnText,
            font: font,
            bg: bg,
            onClick: OnBackToMenuClicked
        );

        BuildOneButton(
            parentRT: parentRT,
            anchoredPos: new Vector2(0f, bottomY),
            text: exitBtnText,
            font: font,
            bg: bg,
            onClick: OnExitClicked
        );
    }

    private void BuildOneButton(
        RectTransform parentRT,
        Vector2 anchoredPos,
        string text,
        Font font,
        Sprite bg,
        System.Action onClick)
    {
        GameObject go = new GameObject("Btn_" + text, typeof(RectTransform));
        go.transform.SetParent(parentRT, false);

        Image img = go.AddComponent<Image>();
        img.sprite = bg;
        img.color = btnBackgroundColor;
        img.type = Image.Type.Sliced;
        img.raycastTarget = true;

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() =>
        {
            // 🔊 暂停面板按钮点击音效
            if (AudioManager.Instance != null)
            {
                var clip = pauseBtnClickSfx != null ? pauseBtnClickSfx : AudioManager.Instance.defaultClickSfx;
                if (clip != null) AudioManager.Instance.PlaySFX(clip);
            }
            onClick?.Invoke();
        });

        RectTransform rt = btn.GetComponent<RectTransform>();
        // 锚点：挂在暂停页中心，按钮本身 pivot 取中心
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = btnSize;
        rt.anchoredPosition = anchoredPos;

        // 按钮内文字（居中）
        GameObject t = new GameObject("Text", typeof(RectTransform));
        t.transform.SetParent(go.transform, false);
        Text label = t.AddComponent<Text>();
        label.font = font;
        label.fontSize = btnFontSize;
        label.color = btnTextColor;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = text;
        label.raycastTarget = false;
        RectTransform trt = label.rectTransform;
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    private void OnBackToMenuClicked()
    {
        // 先 Resume timeScale，否则新场景 Update 不走、画面卡死
        if (pauseTimeScale) Time.timeScale = 1f;
        string scene = resumeSceneName ?? "";
        if (string.IsNullOrEmpty(scene))
        {
            Debug.LogError("[PauseManager] resumeSceneName 为空，无法跳转");
            return;
        }
        Debug.Log($"[PauseManager] 回到主界面: {scene}");
        SceneManager.LoadScene(scene);
    }

    private void OnExitClicked()
    {
        if (pauseTimeScale) Time.timeScale = 1f;
        Debug.Log("[PauseManager] 退出游戏");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static Sprite MakeWhiteSprite()
    {
        Texture2D tex = new Texture2D(4, 4);
        var cols = new Color32[16];
        for (int i = 0; i < 16; i++) cols[i] = new Color32(255, 255, 255, 255);
        tex.SetPixels32(cols);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
    }
}
