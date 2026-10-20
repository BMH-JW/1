using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>一个「文字框 ↔ 内容键」槽位：预制体里摆好的文字框 + 它该显示的内容键。
/// 框的位置/尺寸/字号完全由预制体的 RectTransform 决定，代码只往框里写文字。
/// （需要时仍可勾选下面的覆写开关做微调；不勾就完全不动预制体里的值。）</summary>
[Serializable]
public class PopupTextField
{
    [Tooltip("内容键：代码按这个 key 把内容写进这个框（DailyEvent 可用 title / day / order / purity / content / event / body / orderEvent / purityEvent）")]
    public string fieldName;

    [Tooltip("预制体里的那个文字框 GameObject（TMP_Text 或 Legacy Text 都行）；在预制体里调它的 RectTransform 定范围即可")]
    public GameObject slotGO;

    // Awake 里 TMP→Legacy 后填入（运行时，不出序列化）
    [HideInInspector] public Text text;

    [Tooltip("勾选后覆盖字号")]
    public bool overrideFontSize;
    public int fontSize = 18;

    [Tooltip("勾选后锚点→左上角(0,1) + 覆盖 anchoredPosition；X右正 Y下正")]
    public bool overridePosition;
    public Vector2 anchoredPosition = new Vector2(0f, 0f);

    [Tooltip("勾选后覆盖 sizeDelta (宽高都 0 = 不改尺寸)")]
    public bool overrideSize;
    public Vector2 sizeDelta = new Vector2(0f, 0f);
}

/// <summary>
/// 弹窗基面板：文本/关闭 + 拖动置顶。
/// 拖动弹窗任意空白处（或从按钮上开始拖）即可移动弹窗，
/// 拖动/点击的弹窗自动置顶（SetAsLastSibling），实现"谁被操作谁在最上层"。
///
/// 字体：Awake 时把弹窗里**所有** TMP_Text 运行时换成 Unity 内置 Legacy Text
/// （Font.CreateDynamicFontFromOSFont 路线永远比 TMP FontAsset 稳，不会再有 GlyphRenderMode / materialForRendering 这些坑）。
///
/// 点击翻页：Inspector 勾选 clickToNextPage + 填 clickPageTexts 列表 →
/// 点击弹窗任意空白处自动循环切换正文文字（只有点击没拖动才触发，和拖动天然不冲突）。
/// </summary>
public class PopupPanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler, IPointerClickHandler
{
    /// <summary>来源标记：File3=通关文书弹窗，File2=档案弹窗，空=其他</summary>
    public string popupSource = "";

    // prefab 序列化时的原始引用（TMP_Text 类型，保持不变以兼容预制体）
    public TMP_Text titleText;
    public TMP_Text contentText;
    public Button closeBtn;

    // 运行时转换后的 Legacy Text（Awake 里 TMP→Legacy 之后赋值；SetText 等外部调用都用这两个）
    [HideInInspector] public Text titleLegacy;
    [HideInInspector] public Text contentLegacy;

    [Header("关闭按钮图片")]
    [Tooltip("替换关闭按钮的图片(Sprite)；留空=不改，用预制体原图")]
    public Sprite closeBtnSprite;

    [Header("关闭按钮位置")]
    [Tooltip("勾选后：弹窗打开时把关闭按钮钉在弹窗右上角，再用下面的偏移微调")]
    public bool pinCloseBtnTopRight = true;
    [Tooltip("关闭按钮相对右上角的偏移(像素)：X负=向左远离右边缘，Y负=向下远离上边缘")]
    public Vector2 closeBtnTopRightOffset = new Vector2(-16f, -16f);

    [Header("中文显示")]
    [Tooltip("系统字体名（Font.CreateDynamicFontFromOSFont 用）；默认 Microsoft YaHei")]
    public string systemFontName = "Microsoft YaHei";

    // ================================================================
    // 样式覆写槽位（Inspector 直接调，title 和 content 各一套，互相独立）
    // 每个覆写带一个 bool 开关：勾选 → 用下面的值覆盖；不勾选 → 不动（保持 TMP 转过来的原值）
    // ================================================================

    [Header("▸ 标题样式覆写")]
    [Tooltip("勾选后覆盖标题字号；不勾选=保持 TMP 原值")]
    public bool titleOverrideFontSize;
    public int titleFontSize = 28;

    [Tooltip("勾选后把标题 RectTransform 锚点设为左上，并覆盖 anchoredPosition (X右正 Y上正, 单位像素)")]
    public bool titleOverridePosition;
    public Vector2 titleAnchoredPosition = new Vector2(0f, 0f);

    [Tooltip("勾选后覆盖标题区域尺寸 (sizeDelta, 单位像素)；宽高都设 0 表示只改位置不改尺寸")]
    public bool titleOverrideSize;
    public Vector2 titleSizeDelta = new Vector2(0f, 0f);

    [Header("▸ 正文样式覆写")]
    [Tooltip("勾选后覆盖正文字号；不勾选=保持 TMP 原值")]
    public bool contentOverrideFontSize;
    public int contentFontSize = 18;

    [Tooltip("勾选后把正文 RectTransform 锚点设为左上，并覆盖 anchoredPosition (X右正 Y上正, 单位像素)")]
    public bool contentOverridePosition;
    public Vector2 contentAnchoredPosition = new Vector2(0f, 0f);

    [Tooltip("勾选后覆盖正文区域尺寸 (sizeDelta, 单位像素)")]
    public bool contentOverrideSize;
    public Vector2 contentSizeDelta = new Vector2(0f, 0f);

    [Header("▸ 文字框 ↔ 内容键（多文本框映射）")]
    [Tooltip("每一项 = 预制体里的一个文字框 + 它要显示的内容键。代码只往框里写文字，绝不改框的位置/尺寸/字号。\n" +
        "DailyEvent 弹窗可用键：title / day / order / purity / content（event、body 是同一份） / orderEvent / purityEvent\n" +
        "通关文书：docNo / name / origin / destination / reason / health / day\n" +
        "档案：name / origin / motive / memory / relationship / desire / will / body / bond / drive")]
    public List<PopupTextField> documentFields = new List<PopupTextField>();

    [Header("拖动置顶")]
    [Tooltip("勾选后：按住弹窗拖动可移动位置，被拖动/点击的弹窗自动置顶")]
    public bool dragToFront = true;

    [Header("点击翻页（PopupPanel6 等用）")]
    [Tooltip("勾选后：点击弹窗任意空白处 → 循环切换正文文字（只有点击没拖动才触发）")]
    public bool clickToNextPage = false;
    [TextArea(3, 10)]
    [Tooltip("多页文案列表：点击弹窗时正文会在这些文字之间循环切换。至少填 2 条才有翻页效果")]
    public List<string> clickPageTexts = new List<string>();

    [Header("屏幕自适应（非 16:9 屏幕不被裁切）")]
    [Tooltip("勾选后：弹窗打开时自动保证整体留在可见画布范围内 —— 尺寸超出可见区域时整体等比缩小（不拉伸变形），位置越界时夹回屏幕内")]
    public bool fitInsideScreen = true;
    [Tooltip("弹窗距离画布边缘的最小留白（画布逻辑像素）")]
    public float screenFitMargin = 8f;

    private RectTransform _rt;
    private float _canvasScale = 1f;
    private int _pageIndex = 0;

    // 预制体里摆好的原始尺寸/位置 —— 自适应每次都以这两个原始值重算，
    // 避免反复开合弹窗时尺寸越缩越小、位置越偏越远
    private Vector2 _baseSize;
    private Vector2 _baseAnchoredPosition;
    private bool _baseCaptured;

    void Awake()
    {
        _rt = (RectTransform)transform;

        // -------- 第 1 步：把弹窗里所有 TMP_Text 换成 Legacy Text（运行时转换） --------
        ConvertAllTmpInPopup();

        // -------- 第 2 步：拖动/关闭按钮 --------
        _rt.SetAsLastSibling();
        if (closeBtn != null)
        {
            closeBtn.onClick.AddListener(ClosePanel);
            var btnRT = closeBtn.GetComponent<RectTransform>();
            if (btnRT != null) btnRT.SetAsLastSibling();
            ApplyCloseButtonSprite();
            ApplyCloseButtonPosition();
        }

        // -------- 第 3 步：字体 --------
        ApplyCjkFont();

        // -------- 第 4 步：Inspector 样式覆写（字号/位置/尺寸） --------
        ApplyStyleOverrides();

        // -------- 第 5 步：点击翻页初始化（如果 clickToNextPage=true，第一页就显示 clickPageTexts[0]） --------
        if (clickToNextPage && clickPageTexts != null && clickPageTexts.Count > 0)
        {
            _pageIndex = 0;
            // SetText 里表单弹窗会清 content，但点击翻页的弹窗不是表单（没有 documentFields），所以这里直接设
            if (contentLegacy != null) contentLegacy.text = clickPageTexts[0];
            Debug.Log($"[PopupPanel] 点击翻页初始化 —— {clickPageTexts.Count} 页，当前第 1 页");
        }

        Canvas c = GetComponentInParent<Canvas>();
        if (c != null) _canvasScale = c.rootCanvas.scaleFactor;

        // -------- 第 6 步：屏幕自适应（非 16:9 屏幕把弹窗整体拉回可见范围内） --------
        ApplyScreenFit();
    }

    void OnEnable()
    {
        if (_rt == null) _rt = (RectTransform)transform;
        _rt.SetAsLastSibling();
        if (closeBtn != null)
        {
            var btnRT = closeBtn.GetComponent<RectTransform>();
            if (btnRT != null) btnRT.SetAsLastSibling();
        }
        ApplyCloseButtonPosition();
        ApplyCjkFont();
        ApplyStyleOverrides();
    }

    /// <summary>把这个弹窗里所有 TMP_Text 换成 Legacy Text。
    /// titleText/contentText 原来指向 TMP_Text 组件 → 转换后那些组件被 Destroy，
    /// 我们用同一个 GameObject 上新加的 Legacy Text 组件来赋值 titleLegacy/contentLegacy。</summary>
    private void ConvertAllTmpInPopup()
    {
        GameObject titleGO = titleText != null ? titleText.gameObject : null;
        GameObject contentGO = contentText != null ? contentText.gameObject : null;

        // 同样保存 documentFields 的 slotGO —— 里面的 TMP_Text 也要被转换
        // 只有当文字框不在弹窗层级内（比如被拖到别的面板/Canvas 下）才搬进 popup；
        // 已经在 popup 里（含嵌套子物体）的原样不动 —— 保证预制体里调好的 RectTransform 范围不被改动
        var fieldGOs = new Dictionary<PopupTextField, GameObject>();
        foreach (var f in documentFields)
        {
            if (f == null || f.slotGO == null) continue;
            fieldGOs[f] = f.slotGO;

            var slotRT = f.slotGO.GetComponent<RectTransform>();
            var myRT = (RectTransform)transform;
            if (slotRT != null && !slotRT.IsChildOf(myRT))
            {
                Vector3 worldPos = slotRT.position;
                slotRT.SetParent(myRT, false);  // worldPositionStays=false → 按本地坐标重新对齐
                // 如果 reparent 后位置不对，用 worldPosition 兜一下
                slotRT.position = worldPos;
            }
        }

        var allTmp = new List<TMP_Text>(GetComponentsInChildren<TMP_Text>(true));
        foreach (var t in allTmp)
        {
            if (t == null) continue;
            GameSessionBridge.ConvertTmpToLegacy(t);
        }

        if (titleGO != null) titleLegacy = titleGO.GetComponent<Text>();
        if (contentGO != null) contentLegacy = contentGO.GetComponent<Text>();

        foreach (var kv in fieldGOs)
        {
            var go = kv.Value;
            kv.Key.text = go != null ? go.GetComponent<Text>() : null;
        }

        // 预制体里没直接挂 titleText / contentText 引用时，回落到「文字框 ↔ 内容键」里
        // key=title / key=content 的框 —— 这样规则弹窗（Btn_7 的 {RULES}）等复用同一预制体的
        // 弹窗，正文也能写进 key=content 的框，标题写进 key=title 的框。
        // （必须在上面 f.text 赋值之后再取，否则 f.text 还是 null）
        if (titleLegacy == null) titleLegacy = FindFieldText("title");
        if (contentLegacy == null) contentLegacy = FindFieldText("content");

        if (fieldGOs.Count > 0)
        {
            var status = new string[fieldGOs.Count];
            int i = 0;
            foreach (var f in fieldGOs.Keys)
                status[i++] = $"{f.fieldName ?? "(空)"}→{(f.text != null ? "✓" : "✗")}";
            Debug.Log($"[PopupPanel] documentFields 转换结果: {string.Join(", ", status)}");
        }

        if (allTmp.Count > 0)
            Debug.Log($"[PopupPanel] 转换 TMP_Text → Legacy Text：{allTmp.Count} 个 (popupSource={popupSource})");
    }

    /// <summary>在「文字框 ↔ 内容键」列表里找 fieldName 匹配的框的 Text（找不到返回 null）</summary>
    private Text FindFieldText(string key)
    {
        if (documentFields == null) return null;
        foreach (var f in documentFields)
            if (f != null && f.fieldName == key && f.text != null) return f.text;
        return null;
    }

    /// <summary>这个 Text 是不是「文字框 ↔ 内容键」里的某个框（是的话就别再套用样式覆写，保住预制体里摆好的范围）</summary>
    private bool IsAnyFieldText(Text t)
    {
        if (t == null || documentFields == null) return false;
        foreach (var f in documentFields)
            if (f != null && f.text == t) return true;
        return false;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        ApplyCloseButtonPosition();
        ApplyStyleOverrides();
    }
#endif

    private void ApplyCloseButtonSprite()
    {
        if (closeBtn == null || closeBtnSprite == null) return;
        Image img = closeBtn.targetGraphic as Image;
        if (img == null) img = closeBtn.GetComponentInChildren<Image>();
        if (img == null) return;
        img.sprite = closeBtnSprite;
    }

    private void ApplyCloseButtonPosition()
    {
        if (closeBtn == null || !pinCloseBtnTopRight) return;
        RectTransform rt = closeBtn.GetComponent<RectTransform>();
        if (rt == null) return;
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = closeBtnTopRightOffset;
    }

    /// <summary>
    /// 屏幕自适应：保证整个弹窗都落在可见画布（＝屏幕）范围内。
    /// 画面比例不是 16:9 时（带鱼屏 / 更方的屏幕），画布逻辑尺寸会在某一方向上小于设计尺寸 1920x1080，
    /// 固定尺寸 + 固定位置的弹窗就会被裁掉一块（连右上角关闭按钮都可能跑到屏幕外）。
    /// 这里先整体等比缩小到能装下（不变形），再把位置夹回可见范围内。
    /// </summary>
    public void ApplyScreenFit()
    {
        if (!fitInsideScreen) return;
        if (_rt == null) _rt = (RectTransform)transform;

        RectTransform parentRT = _rt.parent as RectTransform;
        if (parentRT == null) return;

        Vector2 canvas = parentRT.rect.size;
        // 父级不是"整屏"级别的容器（弹窗被塞进小面板）时不插手，避免被误缩小
        if (canvas.x < 400f || canvas.y < 400f) return;

        // 以预制体里摆好的原始尺寸/位置为基准，反复调用也不会越缩越小、越偏越远
        if (!_baseCaptured)
        {
            _baseSize = _rt.rect.size;
            _baseAnchoredPosition = _rt.anchoredPosition;
            _baseCaptured = true;
        }
        if (_baseSize.x <= 1f || _baseSize.y <= 1f) return;

        float availW = Mathf.Max(1f, canvas.x - screenFitMargin * 2f);
        float availH = Mathf.Max(1f, canvas.y - screenFitMargin * 2f);

        // 1. 等比缩小到能装下 —— 永远不拉伸，弹窗和文字比例一致
        float scale = Mathf.Min(1f, availW / _baseSize.x, availH / _baseSize.y);
        _rt.localScale = new Vector3(scale, scale, 1f);

        // 锚点被拉伸时尺寸与位置都由父级决定，只做缩放即可
        if (_rt.anchorMin != _rt.anchorMax) return;

        // 2. 位置夹回可见范围内（单点锚点：父级局部坐标 = 锚点参考点 + anchoredPosition）
        Vector2 anchorRef = (Vector2)parentRT.rect.min + Vector2.Scale(parentRT.rect.size, _rt.anchorMin);
        Vector2 center = anchorRef + _baseAnchoredPosition;
        Vector2 half = _baseSize * scale * 0.5f;

        float minX = screenFitMargin + half.x, maxX = canvas.x - screenFitMargin - half.x;
        float minY = screenFitMargin + half.y, maxY = canvas.y - screenFitMargin - half.y;

        // 留白比可用区域还大时（极端窄屏）直接居中
        center.x = minX <= maxX ? Mathf.Clamp(center.x, minX, maxX) : canvas.x * 0.5f;
        center.y = minY <= maxY ? Mathf.Clamp(center.y, minY, maxY) : canvas.y * 0.5f;

        _rt.anchoredPosition = center - anchorRef;
    }

    /// <summary>
    /// 给弹窗内**所有** Legacy Text 挂上含中文字形的字体（现在 TMP 已全换成 Legacy，所以只扫 Text）。
    /// 优先用 GameSessionBridge.GlobalCjkLegacyFont（RoomScene 启动时已创建），
    /// 没有就自己 Font.CreateDynamicFontFromOSFont 兜底创建。
    /// </summary>
    private void ApplyCjkFont()
    {
        Font legacyFont = GameSessionBridge.GlobalCjkLegacyFont;
        if (legacyFont == null) legacyFont = CreateRuntimeLegacyCjkFont();
        if (legacyFont == null) return;

        foreach (var t in GetComponentsInChildren<UnityEngine.UI.Text>(true))
            if (t.font != legacyFont) t.font = legacyFont;
    }

    /// <summary>Font.CreateDynamicFontFromOSFont —— Unity 内置，任何 Windows 机器必通</summary>
    private Font CreateRuntimeLegacyCjkFont()
    {
        string[] candidates = { systemFontName, "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "SimSun", "宋体", "Arial Unicode MS", "Arial" };
        for (int i = 0; i < candidates.Length; i++)
        {
            if (string.IsNullOrEmpty(candidates[i])) continue;
            Font f = Font.CreateDynamicFontFromOSFont(candidates[i], 48);
            if (f != null) return f;
        }
        Debug.LogWarning("[PopupPanel] 找不到可用的系统中文字体，中文会显示成方框。");
        return null;
    }

    /// <summary>Inspector 样式覆写入口：title 和 content 各自独立。
    /// 每个覆写带 bool 开关，勾选才应用 —— 不勾选 = 保持 TMP 转换过来的原值不动。</summary>
    private void ApplyStyleOverrides()
    {
        // ---- 标题 ----
        // 若 titleLegacy 是回落到 key=title 的文字框，则不套用下面的覆写 ——
        // 那个框的范围由预制体决定，不能被覆写参数挪走。
        if (titleLegacy != null && !IsAnyFieldText(titleLegacy))
        {
            if (titleOverrideFontSize) titleLegacy.fontSize = titleFontSize;

            var titleRT = titleLegacy.GetComponent<RectTransform>();
            if (titleRT != null)
            {
                if (titleOverridePosition) SetAnchorTopLeft(titleRT, titleAnchoredPosition);
                if (titleOverrideSize) titleRT.sizeDelta = titleSizeDelta;
            }
        }

        // ---- 正文 ----
        // 同上：contentLegacy 回落到 key=content 的文字框时不套用覆写，保住预制体里摆好的范围。
        if (contentLegacy != null && !IsAnyFieldText(contentLegacy))
        {
            if (contentOverrideFontSize) contentLegacy.fontSize = contentFontSize;

            var contentRT = contentLegacy.GetComponent<RectTransform>();
            if (contentRT != null)
            {
                if (contentOverridePosition) SetAnchorTopLeft(contentRT, contentAnchoredPosition);
                if (contentOverrideSize) contentRT.sizeDelta = contentSizeDelta;
            }
        }

        // ---- documentFields（通关文书/档案各字段）----
        foreach (var f in documentFields)
        {
            if (f == null || f.text == null) continue;

            if (f.overrideFontSize) f.text.fontSize = f.fontSize;

            var rt = f.text.GetComponent<RectTransform>();
            if (rt != null)
            {
                if (f.overridePosition) SetAnchorTopLeft(rt, f.anchoredPosition);
                if (f.overrideSize) rt.sizeDelta = f.sizeDelta;
            }
        }
    }

    /// <summary>把 RectTransform 的锚点 + pivot 统一设为左上角 (0,1)，然后 anchoredPosition 用用户给的值。
    /// 这样 Inspector 里的 X 就是向右几像素，Y 就是向下几像素 —— 直觉友好。</summary>
    static void SetAnchorTopLeft(RectTransform rt, Vector2 anchoredPos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = anchoredPos;
    }

    public void SetSource(string source)
    {
        popupSource = source;
    }

    /// <summary>设置标题和正文。
    /// 只有通关文书(File3) / 人物档案(File2) 这类"表单弹窗"才跳过正文注入
    /// —— 它们的动态数据全部由 SetDocumentFields 逐字段填，再塞一遍大正文块会在背景图上叠两层字。
    /// 其余复用同一预制体的弹窗（规则弹窗 Btn_7、DailyEvent、结算等）正文照常写入 ——
    /// 正文槽位没挂引用时，Awake 里已回落到 key=content 的文字框。</summary>
    public void SetText(string title, string content)
    {
        if (titleLegacy != null) titleLegacy.text = title;
        else if (titleText != null) titleText.text = title;

        bool isFormPanel = documentFields != null && documentFields.Count > 0
            && (popupSource == "File2" || popupSource == "File3");
        bool isPaged = !isFormPanel && clickToNextPage && clickPageTexts != null && clickPageTexts.Count > 0;

        if (isFormPanel)
        {
            // 表单弹窗：正文槽位不再塞大段文字 —— 数据走 documentFields 逐字段填
            if (contentLegacy != null) contentLegacy.text = "";
            else if (contentText != null) contentText.text = "";
        }
        else if (isPaged)
        {
            // 点击翻页弹窗（PopupPanel6 等）：正文固定显示第一页。
            // 否则外部（RoomInteract）传进来的空 content 会把 Awake 里设好的第一页清掉
            // → 弹窗打开时是空白，要点一下才出字。
            _pageIndex = 0;
            string first = clickPageTexts[0];
            if (contentLegacy != null) contentLegacy.text = first;
            else if (contentText != null) contentText.text = first;
        }
        else
        {
            if (contentLegacy != null) contentLegacy.text = content;
            else if (contentText != null) contentText.text = content;

            // 复用同一预制体但没走表单数据（规则弹窗 Btn_7 / GamerOver 等）时：
            // 把「文字框 ↔ 内容键」里跟本弹窗无关的框先隐藏，免得露出预制体里的示例文字（New Text / Button）。
            // 只让等于 titleLegacy（且有标题）或等于 contentLegacy（且有正文）的框保持可见。
            if (documentFields != null && documentFields.Count > 0)
            {
                foreach (var f in documentFields)
                {
                    if (f == null || f.slotGO == null) continue;
                    bool isTitleBox = f.text != null && f.text == titleLegacy && !string.IsNullOrEmpty(title);
                    bool isContentBox = f.text != null && f.text == contentLegacy && !string.IsNullOrEmpty(content);
                    f.slotGO.SetActive(isTitleBox || isContentBox);
                }
            }
        }
    }

    /// <summary>按 fieldName 匹配填入各个 documentFields 的值。
    /// 只有 Dictionary 里 key 命中 fieldName 的才会更新 —— 其他字段保持不动。
    /// 用法：panel.SetDocumentFields(new Dictionary{"name", doc.name}, {"origin", doc.sourceDisplayName});</summary>
    public void SetDocumentFields(IDictionary<string, string> values)
    {
        if (values == null || values.Count == 0) return;

        var docFields = documentFields ?? new List<PopupTextField>();
        Debug.Log($"[PopupPanel] SetDocumentFields: popupSource={popupSource}, dict 里 {values.Count} 个 key, documentFields 列表里 {docFields.Count} 个 fieldName");

        foreach (var f in docFields)
        {
            if (f == null) continue;
            if (string.IsNullOrEmpty(f.fieldName))
            {
                Debug.LogWarning($"[PopupPanel] 有个 PopupTextField 的 fieldName 是空的，Inspector 里填一下");
                continue;
            }
            if (f.text == null)
            {
                Debug.LogWarning($"[PopupPanel] fieldName={f.fieldName} 的 slotGO 里没拿到 Text 组件 —— 确认 slotGO 不是空引用，且里面有 Legacy Text 或 TMP Text");
                continue;
            }
            if (values.TryGetValue(f.fieldName, out string v))
            {
                f.text.text = v ?? "";
                // 有值才把这个框显出来（只做"显示"，不做"隐藏" ——
                // 免得把 File2/File3 表单里当作容器用的框误关掉）；
                // DailyEvent 那边已由 SetText("","") 先把所有框隐藏，这里按值放出来即可。
                if (!string.IsNullOrEmpty(v) && f.slotGO != null) f.slotGO.SetActive(true);
                Debug.Log($"  ✓ {f.fieldName} = \"{v}\"");
            }
            else
            {
                Debug.LogWarning($"  ✗ {f.fieldName} —— dict 里找不到这个 key！dict keys 是: [{string.Join(", ", values.Keys)}]");
            }
        }
    }

    /// <summary>外部想直接访问正文 Text 组件时用这个（PopupTabs.cs 等）</summary>
    public Text GetContentLegacy() { return contentLegacy; }
    public Text GetTitleLegacy()  { return titleLegacy; }

    public void ClosePanel()
    {
        RoomInteract.Untrack(gameObject);
        Destroy(gameObject);
    }

    // ================================================================
    // 拖动置顶
    // ================================================================
    public void OnPointerDown(PointerEventData eventData)
    {
        if (dragToFront) _rt.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragToFront) _rt.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragToFront) return;
        _rt.anchoredPosition += eventData.delta / _canvasScale;
    }

    /// <summary>点击翻页 —— 只有点击没拖动才触发（Unity 天然保证 PointerClick 不在 Drag 时触发）。
    /// 循环切换 clickPageTexts 列表里的正文。</summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (!clickToNextPage) return;
        if (clickPageTexts == null || clickPageTexts.Count <= 1) return;

        _pageIndex = (_pageIndex + 1) % clickPageTexts.Count;
        if (contentLegacy != null)
        {
            contentLegacy.text = clickPageTexts[_pageIndex];
            Debug.Log($"[PopupPanel] 点击翻页 → 第 {_pageIndex + 1}/{clickPageTexts.Count} 页");
        }
    }
}
