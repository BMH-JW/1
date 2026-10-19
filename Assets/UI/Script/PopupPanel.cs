using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>一个可单独配置样式的表单文本槽位。
/// prefab 上放 TMP_Text → Awake 时自动转 Legacy Text → 这里的 text 字段就是转换后的 Legacy Text。
/// 每个槽位可独立开关字号/位置/尺寸覆写（bool 开关 = false 时不动，保持原值）。</summary>
[Serializable]
public class PopupTextField
{
    [Tooltip("字段标识名（SetDocumentFields 时用这个 key 匹配 Dictionary）")]
    public string fieldName;

    [Tooltip("prefab 上放数据的 GameObject（里面必须有一个 TMP_Text 组件）")]
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
/// </summary>
public class PopupPanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
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

    [Header("▸ 表单数据槽位（通关文书/档案分字段填入）")]
    [Tooltip("列表里每一个都是一个独立数据字段的 Text 槽位；RoomInteract.SetDocumentFields 按 fieldName 匹配填值。\n通关文书参考：docNo / name / origin / destination / reason / health / day\n档案参考：name / origin / motive / memory / relationship / desire / will / body / bond / drive")]
    public List<PopupTextField> documentFields = new List<PopupTextField>();

    [Header("拖动置顶")]
    [Tooltip("勾选后：按住弹窗拖动可移动位置，被拖动/点击的弹窗自动置顶")]
    public bool dragToFront = true;

    private RectTransform _rt;
    private float _canvasScale = 1f;

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

        Canvas c = GetComponentInParent<Canvas>();
        if (c != null) _canvasScale = c.rootCanvas.scaleFactor;
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
        // 关键：无论 slotGO 当前挂在哪（哪怕在 Canvas 下），自动 reparent 到 popup 下
        // 否则 anchor 相对于 Canvas，popup 拖动时 Text 不动
        var fieldGOs = new Dictionary<PopupTextField, GameObject>();
        foreach (var f in documentFields)
        {
            if (f == null || f.slotGO == null) continue;
            fieldGOs[f] = f.slotGO;

            // 自动 reparent：确保 Text 槽位的父物体是本 popup —— 这样 anchor 相对于 popup，popup 拖动 Text 跟着动
            var slotRT = f.slotGO.GetComponent<RectTransform>();
            var myRT = (RectTransform)transform;
            if (slotRT != null && slotRT.parent != myRT)
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
        if (titleLegacy != null)
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
        if (contentLegacy != null)
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
    /// 关键：如果 documentFields 列表不为空（= 这是个"表单弹窗"，通关文书/档案），
    /// 就跳过 content 的注入 —— 因为所有动态数据都由 SetDocumentFields 逐字段填，
    /// 再塞一遍 ResolveContent 的大正文块就会在背景图上叠两层字。
    /// title 照常设（弹窗的大标题，比如"通关文书"/"人物档案"）。</summary>
    public void SetText(string title, string content)
    {
        if (titleLegacy != null) titleLegacy.text = title;
        else if (titleText != null) titleText.text = title;

        bool isFormPanel = documentFields != null && documentFields.Count > 0;
        if (isFormPanel)
        {
            // 表单弹窗：正文槽位不再塞大段文字 —— 数据走 documentFields 逐字段填
            if (contentLegacy != null) contentLegacy.text = "";
            else if (contentText != null) contentText.text = "";
        }
        else
        {
            if (contentLegacy != null) contentLegacy.text = content;
            else if (contentText != null) contentText.text = content;
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
}
