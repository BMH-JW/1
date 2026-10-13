using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 新手引导管理器（自包含，挂在场景任意物体上）
/// 阶段0 引导NPC打字机对话 → 阶段1~8 发光提示框依次框选铃铛/邮箱/档案/文书/印章/电话/信息屏/时钟
/// 引导结束后进入自由审批；8位NPC审批完成后自动弹出结算弹窗。
/// 目标 = Inspector拖拽(优先) / 按 findName 自动查找(支持逗号分隔多个备选名，忽略大小写)。
/// 气泡尺寸与各阶段文案均为Inspector槽位。
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    /// <summary>引导目标：手动拖拽优先，否则按名字查找(逗号分隔备选名)</summary>
    [Serializable]
    public class GuideTarget
    {
        [Tooltip("手动拖拽赋值(优先)")]
        public GameObject target;
        [Tooltip("target为空时按名字查找；多个备选名用英文逗号分隔，命中任意一个当前激活的即可")]
        public string findName;
    }

    private enum Stage { None, Welcome, Bell, MailBox, Profile, Document, Stamps, Phone, Screen, Clock, FreePlay }

    [Header("阶段0 引导对话")]
    public Sprite guideNpcSprite;                 // 引导NPC形象(可选)
    [Range(0.01f, 0.3f)] public float typingInterval = 0.06f; // 打字机速度(秒/字)
    [TextArea(2, 4)] public string welcomeText = "欢迎来到深空当铺。我会一步步教你这里的工作规则。";
    [TextArea(2, 4)] public string welcomeText2 = "跟着金色高亮提示操作，即可完成基础引导。";

    [Header("引导目标(能拖就拖；拖不了就把findName改成场景里的实际名字)")]
    public GuideTarget bell = new GuideTarget { findName = "Btn_Bell" };
    [Tooltip("客人NPC，运行时由铃铛生成")]
    public GuideTarget npcGuest = new GuideTarget { findName = "npcObj" };
    [Tooltip("新旧邮箱谁激活就高亮谁")]
    public GuideTarget mailbox = new GuideTarget { findName = "Mail,imageMailBox2,imageMailBox" };
    public GuideTarget profile = new GuideTarget { findName = "Btn_File3" };
    public GuideTarget document = new GuideTarget { findName = "Btn_File2" };
    public GuideTarget stamp1 = new GuideTarget { findName = "Btn_Stamp1" };   // 通过章
    public GuideTarget stamp2 = new GuideTarget { findName = "Btn_Stamp2" };   // 不通过章
    public GuideTarget phone = new GuideTarget { findName = "Btn_TelePhone" }; // 电话(扣留)
    public GuideTarget screen = new GuideTarget { findName = "InfoScreen" };
    public GuideTarget clock = new GuideTarget { findName = "Clock" };

    [Header("气泡尺寸与文案(全部可改)")]
    [Tooltip("引导气泡的宽高")]
    public Vector2 bubbleSize = new Vector2(560f, 160f);
    [TextArea(2, 4)] public string bellText = "点击铃铛，呼叫客人前来";
    [TextArea(2, 4)] public string mailBoxText = "点击纸盒，查看客人携带的物品信息";
    [TextArea(2, 4)] public string profileText = "打开人物档案，留声机会播报客人背景信息，留意各项数值";
    [TextArea(2, 4)] public string documentText = "查看审批规则：选择对世界影响更小的方案即为正确审批";
    [TextArea(2, 4)] public string stampsText = "两枚印章代表你的审批选择，谨慎做出决定";
    [TextArea(2, 4)] public string phoneText = "难以判断时，可以选择扣留，点击电话确认操作";
    [TextArea(2, 4)] public string screenText = "在这里查看事件与各个世界的背景信息";
    [TextArea(2, 4)] public string clockText = "注意时间流逝，务必在下班前完成审批";

    [Header("提示框外观(全部可改)")]
    [Tooltip("气泡/面板图片，留空用代码生成的圆角白底")]
    public Sprite bubbleSpriteOverride;
    [Tooltip("高亮描边颜色")]
    public Color highlightColor = new Color(1f, 0.85f, 0.3f, 1f);
    [Tooltip("闪烁频率(次/秒)")]
    [Range(0.5f, 10f)] public float pulseFrequency = 4f;
    [Tooltip("闪烁幅度0~1")]
    [Range(0f, 1f)] public float pulseAmplitude = 0.3f;
    [Tooltip("提示框比目标大一圈的内边距")]
    public float highlightPadding = 12f;

    [Header("文字外观")]
    public int bubbleFontSize = 26;
    public Color textColor = new Color(0.15f, 0.12f, 0.08f, 1f);
    [Tooltip("留空用内置回退字体")]
    public Font fontOverride;

    [Header("NPC离场与结算")]
    public float leaveAnimDuration = 0.6f;
    public int totalNpcCount = 8;

    // ---------------- 运行时状态 ----------------
    private Stage stage = Stage.None;
    private readonly HashSet<GameObject> hookedTargets = new HashSet<GameObject>(); // 持久挂钩防重复
    private readonly List<Image> frames = new List<Image>();                        // 高亮框
    private readonly List<GuideTarget> frameGts = new List<GuideTarget>();          // 高亮框对应目标(含备选名)
    private readonly List<StageHook> stageHooks = new List<StageHook>();            // 阶段推进钩子
    private UnityAction stageOnNext;                                                // 当前阶段点击目标后的推进动作

    private struct StageHook
    {
        public Button btn;
        public UnityAction action;
    }

    private RectTransform overlayRect;
    private RectTransform bubbleRect;
    private Text bubbleText;
    private Sprite fillSprite, borderSprite;

    private GameObject dialogRoot;
    private Text dialogText;
    private Coroutine dialogCoroutine;
    private bool dialogTyping;
    private int dialogStep;

    private Coroutine leaveCoroutine;
    private GameObject settlementRoot;
    private bool settlementShown;
    private int completedCount, correctCount, wrongCount, corruptionCount;

    // ================================================================
    // 生命周期
    // ================================================================
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        BuildOverlay();
        TryBindPersistentHooks();
        StartCoroutine(EnterWelcomeNextFrame());
    }

    private IEnumerator EnterWelcomeNextFrame()
    {
        yield return null; // 等一帧让场景UI先初始化
        EnterStage(Stage.Welcome);
    }

    void Update()
    {
        if (frames.Count == 0 || overlayRect == null) return;

        // 呼吸发光
        float pulse = (1f - pulseAmplitude) + pulseAmplitude * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseFrequency * Mathf.PI * 2f));
        Color c = highlightColor;
        c.a *= pulse;

        bool anyVisible = false;
        for (int i = 0; i < frames.Count; i++)
        {
            Image f = frames[i];
            if (f == null) continue;

            GuideTarget gt = frameGts[i];
            GameObject t = gt != null ? gt.target : null;

            // 目标隐藏了：在备选名里找当前激活的(如旧邮箱→新邮箱切换)，指示自动跟过去
            if (t == null || !t.activeInHierarchy)
            {
                if (gt != null)
                {
                    GameObject alt = ResolveTarget(gt, true);
                    if (alt != null && alt != t)
                    {
                        t = alt;
                        if (stageOnNext != null) HookStage(alt, stageOnNext); // 新目标也要能点击推进
                    }
                }
            }

            if (t == null || !t.activeInHierarchy)
            {
                f.enabled = false;
                continue;
            }
            f.enabled = true;
            ApplyFrameRect(f.rectTransform, t.transform as RectTransform);
            f.color = c;
            anyVisible = true;
        }

        // 目标全部隐藏时气泡也跟着藏
        if (bubbleRect != null) bubbleRect.gameObject.SetActive(anyVisible);
    }

    // ================================================================
    // 目标解析
    // ================================================================
    private string[] Names(GuideTarget gt)
    {
        if (gt == null || string.IsNullOrEmpty(gt.findName)) return new string[0];
        string[] parts = gt.findName.Split(',');
        List<string> list = new List<string>();
        foreach (string p in parts)
        {
            string n = p.Trim();
            if (!string.IsNullOrEmpty(n)) list.Add(n);
        }
        return list.ToArray();
    }

    /// <summary>解析目标。requireActive=true时只认激活物体(供调用方轮询等待)</summary>
    private GameObject ResolveTarget(GuideTarget gt, bool requireActive = false)
    {
        if (gt == null) return null;

        if (gt.target != null)
        {
            if (!requireActive || gt.target.activeInHierarchy) return gt.target;
        }

        string[] names = Names(gt);
        if (names.Length == 0) return null;

        if (requireActive)
        {
            foreach (string n in names)
            {
                GameObject found = GameObject.Find(n); // 只查激活物体，开销小
                if (found != null) { gt.target = found; return found; }
            }
            return null;
        }

        for (int pass = 0; pass < 2; pass++) // 先激活后隐藏
        {
            foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.IsValid()) continue; // 排除预制体资源
                bool active = go.activeInHierarchy;
                if (pass == 0 && !active) continue;
                if (pass == 1 && active) continue;
                foreach (string n in names)
                {
                    if (string.Equals(go.name, n, StringComparison.OrdinalIgnoreCase))
                    {
                        Debug.Log($"[Tutorial] 自动找到目标：{n}");
                        gt.target = go;
                        return go;
                    }
                }
            }
        }
        Debug.LogWarning($"[Tutorial] 未找到目标[{gt.findName}]，请检查名字或手动拖拽赋值");
        return null;
    }

    /// <summary>忽略大小写的激活物体全量扫描(GameObject.Find的兜底，较慢，低频调用)</summary>
    private GameObject FindActiveByNameLoose(string[] names)
    {
        if (names == null || names.Length == 0) return null;
        foreach (GameObject go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go.scene.IsValid() || !go.activeInHierarchy) continue;
            foreach (string n in names)
            {
                if (string.Equals(go.name, n, StringComparison.OrdinalIgnoreCase)) return go;
            }
        }
        return null;
    }

    // ================================================================
    // 覆盖层Canvas
    // ================================================================
    private void BuildOverlay()
    {
        GameObject root = new GameObject("TutorialOverlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        root.transform.SetParent(transform, false);
        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 30000;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        overlayRect = root.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
    }

    private Vector2 WorldToOverlayLocal(Vector3 world)
    {
        Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, world);
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(overlayRect, sp, null, out local);
        return local;
    }

    // ================================================================
    // 阶段0：打字机对话
    // ================================================================
    private void BuildDialog()
    {
        CloseDialog(); // 清掉可能残留的旧对话UI

        dialogRoot = new GameObject("TutorialDialog", typeof(RectTransform), typeof(Image), typeof(Button));
        dialogRoot.transform.SetParent(overlayRect, false);
        RectTransform dr = dialogRoot.GetComponent<RectTransform>();
        dr.anchorMin = Vector2.zero; dr.anchorMax = Vector2.one;
        dr.offsetMin = Vector2.zero; dr.offsetMax = Vector2.zero;
        Image dim = dialogRoot.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.35f);
        dialogRoot.GetComponent<Button>().onClick.AddListener(OnDialogClick);

        // 引导NPC形象(可选)
        if (guideNpcSprite != null)
        {
            GameObject npcGo = new GameObject("GuideNpc", typeof(RectTransform), typeof(Image));
            npcGo.transform.SetParent(dr, false);
            Image img = npcGo.GetComponent<Image>();
            img.sprite = guideNpcSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            RectTransform nr = npcGo.GetComponent<RectTransform>();
            nr.anchorMin = new Vector2(0f, 0f); nr.anchorMax = new Vector2(0f, 0f);
            nr.pivot = new Vector2(0f, 0f);
            nr.anchoredPosition = new Vector2(140f, 130f);
            nr.sizeDelta = new Vector2(320f, 320f);
        }

        // 对话气泡(文本直接挂气泡，禁止套零尺寸容器导致竖排)
        GameObject bubble = new GameObject("DialogBubble", typeof(RectTransform), typeof(Image));
        bubble.transform.SetParent(dr, false);
        Image bImg = bubble.GetComponent<Image>();
        bImg.sprite = BubbleSprite;
        bImg.type = Image.Type.Sliced;
        bImg.raycastTarget = false;
        RectTransform br = bubble.GetComponent<RectTransform>();
        br.anchorMin = new Vector2(0.5f, 0f); br.anchorMax = new Vector2(0.5f, 0f);
        br.pivot = new Vector2(0.5f, 0f);
        br.anchoredPosition = new Vector2(0f, 160f);
        br.sizeDelta = new Vector2(Mathf.Max(300f, bubbleSize.x * 1.3f), Mathf.Max(120f, bubbleSize.y));

        dialogText = CreateText("", br, bubbleFontSize, textColor, TextAnchor.MiddleLeft, FontStyle.Normal);
        RectTransform tr = dialogText.rectTransform;
        tr.offsetMin = new Vector2(30f, 18f);
        tr.offsetMax = new Vector2(-30f, -18f);
    }

    private IEnumerator TypeDialog(string line)
    {
        dialogTyping = true;
        dialogText.text = "";
        foreach (char ch in line)
        {
            dialogText.text += ch;
            yield return new WaitForSeconds(typingInterval);
        }
        dialogTyping = false;
    }

    /// <summary>点击对话：播放中→瞬间全文；已全文→下一段/关闭进阶段1</summary>
    private void OnDialogClick()
    {
        string[] lines = { welcomeText, welcomeText2 };
        if (dialogTyping)
        {
            if (dialogCoroutine != null) StopCoroutine(dialogCoroutine);
            dialogCoroutine = null;
            dialogTyping = false;
            dialogText.text = lines[dialogStep];
            return;
        }
        dialogStep++;
        if (dialogStep < lines.Length && !string.IsNullOrEmpty(lines[dialogStep]))
        {
            dialogCoroutine = StartCoroutine(TypeDialog(lines[dialogStep]));
        }
        else
        {
            CloseDialog();
            EnterStage(Stage.Bell);
        }
    }

    private void CloseDialog()
    {
        if (dialogCoroutine != null) { StopCoroutine(dialogCoroutine); dialogCoroutine = null; }
        if (dialogRoot != null) Destroy(dialogRoot);
        dialogRoot = null;
        dialogText = null;
        dialogTyping = false;
    }

    // ================================================================
    // 阶段1~8：发光提示框 + 气泡
    // ================================================================
    private void EnterStage(Stage s)
    {
        stage = s;
        stageOnNext = null;
        ClearStageHooks();
        HideHint();

        switch (s)
        {
            case Stage.Welcome:
                BuildDialog();
                dialogStep = 0;
                dialogCoroutine = StartCoroutine(TypeDialog(welcomeText));
                break;
            case Stage.Bell:
                ShowStage(bell, bellText, false, () => EnterStage(Stage.MailBox));
                break;
            case Stage.MailBox:
                // 新旧邮箱谁激活就高亮谁，指示立即出现
                ShowStage(mailbox, mailBoxText, false, () => EnterStage(Stage.Profile));
                break;
            case Stage.Profile:
                ShowStage(profile, profileText, true, () => EnterStage(Stage.Document));
                break;
            case Stage.Document:
                ShowStage(document, documentText, true, () => EnterStage(Stage.Stamps));
                break;
            case Stage.Stamps:
                ShowStage(new GuideTarget[] { stamp1, stamp2 }, stampsText, false, null, TryBindPersistentHooks);
                break;
            case Stage.Phone:
                ShowStage(phone, phoneText, false, null, TryBindPersistentHooks);
                break;
            case Stage.Screen:
                ShowStage(screen, screenText, false, () => EnterStage(Stage.Clock));
                break;
            case Stage.Clock:
                ShowStage(clock, clockText, false, FinishGuide);
                break;
        }
    }

    private void ShowStage(GuideTarget gt, string text, bool dragHint, UnityAction onNext, Action afterReady = null)
    {
        StartCoroutine(ShowStageRoutine(new GuideTarget[] { gt }, text, dragHint, onNext, afterReady));
    }

    private void ShowStage(GuideTarget[] gts, string text, bool dragHint, UnityAction onNext, Action afterReady = null)
    {
        StartCoroutine(ShowStageRoutine(gts, text, dragHint, onNext, afterReady));
    }

    /// <summary>
    /// 等待目标激活后显示提示。目标未出现时每0.1秒轮询，
    /// GameObject.Find未命中时定期做忽略大小写的全量扫描兜底。
    /// </summary>
    private IEnumerator ShowStageRoutine(GuideTarget[] gts, string text, bool dragHint, UnityAction onNext, Action afterReady)
    {
        Stage myStage = stage;
        stageOnNext = onNext;
        bool warned = false;
        int tick = 0;

        while (true)
        {
            if (stage != myStage) yield break; // 阶段已被切换

            bool allReady = true;
            foreach (GuideTarget gt in gts)
            {
                if (ResolveTarget(gt, true) == null) { allReady = false; break; }
            }

            if (!allReady && ++tick % 5 == 0)
            {
                // GameObject.Find可能因大小写/名字差异未命中，低频全量扫描兜底
                foreach (GuideTarget gt in gts)
                {
                    if (gt.target != null && gt.target.activeInHierarchy) continue;
                    GameObject loose = FindActiveByNameLoose(Names(gt));
                    if (loose != null) gt.target = loose;
                }
                allReady = true;
                foreach (GuideTarget gt in gts)
                {
                    if (gt.target == null || !gt.target.activeInHierarchy) { allReady = false; break; }
                }
            }

            if (allReady) break;

            if (!warned)
            {
                warned = true;
                Debug.Log($"[Tutorial] 等待目标出现：{gts[0].findName} (目标激活后自动继续)");
            }
            yield return new WaitForSeconds(0.1f);
        }

        if (stage != myStage) yield break;

        ShowHint(gts, text, dragHint);

        if (onNext != null)
        {
            foreach (GuideTarget gt in gts)
            {
                if (gt.target != null) HookStage(gt.target, onNext);
            }
        }
        if (afterReady != null) afterReady();
    }

    private void ShowHint(GuideTarget[] gts, string text, bool dragHint)
    {
        HideHint();

        int idx = 0;
        foreach (GuideTarget gt in gts)
        {
            GameObject go = gt != null ? gt.target : null;
            if (go == null) continue;
            GameObject f = new GameObject("GuideFrame" + idx++, typeof(RectTransform), typeof(Image));
            f.transform.SetParent(overlayRect, false);
            Image img = f.GetComponent<Image>();
            img.sprite = BorderSprite;
            img.type = Image.Type.Sliced;
            img.raycastTarget = false;
            img.color = highlightColor;
            RectTransform fr = f.GetComponent<RectTransform>();
            fr.anchorMin = new Vector2(0.5f, 0.5f); fr.anchorMax = new Vector2(0.5f, 0.5f);
            fr.pivot = new Vector2(0f, 0f);
            fr.sizeDelta = Vector2.zero;
            frames.Add(img);
            frameGts.Add(gt);
        }

        if (frameGts.Count == 0)
        {
            Debug.LogWarning("[Tutorial] 没有可高亮的目标，阶段停住等待");
            return;
        }

        // "可拖动"直接写进气泡(金色小字一行)
        string full = text;
        if (dragHint)
        {
            string hex = ColorUtility.ToHtmlStringRGB(highlightColor);
            int smallSize = Mathf.Max(16, bubbleFontSize - 6);
            full += $"\n<size={smallSize}><color=#{hex}>可拖动</color></size>";
        }

        GameObject b = new GameObject("GuideBubble", typeof(RectTransform), typeof(Image));
        b.transform.SetParent(overlayRect, false);
        Image bImg = b.GetComponent<Image>();
        bImg.sprite = BubbleSprite;
        bImg.type = Image.Type.Sliced;
        bImg.raycastTarget = false;
        bubbleRect = b.GetComponent<RectTransform>();
        bubbleRect.anchorMin = new Vector2(0.5f, 0.5f); bubbleRect.anchorMax = new Vector2(0.5f, 0.5f);
        // 尺寸走Inspector槽位，不再自动量高
        bubbleRect.sizeDelta = new Vector2(Mathf.Max(200f, bubbleSize.x), Mathf.Max(60f, bubbleSize.y));

        // 文本直接挂气泡(不要中间容器，否则宽度为0会竖排)
        bubbleText = CreateText(full, bubbleRect, bubbleFontSize, textColor, TextAnchor.MiddleLeft, FontStyle.Normal);
        RectTransform tr = bubbleText.rectTransform;
        tr.offsetMin = new Vector2(24f, 14f);
        tr.offsetMax = new Vector2(-24f, -14f);

        // 气泡放目标上方，放不下放下方
        RectTransform firstRT = frameGts[0].target.transform as RectTransform;
        if (firstRT != null)
        {
            Vector3[] wc = new Vector3[4];
            firstRT.GetWorldCorners(wc);
            Vector2 top = WorldToOverlayLocal((wc[1] + wc[2]) * 0.5f);
            Vector2 bottom = WorldToOverlayLocal((wc[0] + wc[3]) * 0.5f);
            float halfH = overlayRect.rect.height * 0.5f;
            if (top.y + bubbleRect.rect.height + 30f < halfH)
            {
                bubbleRect.pivot = new Vector2(0.5f, 0f);
                bubbleRect.anchoredPosition = new Vector2(top.x, top.y + 18f);
            }
            else
            {
                bubbleRect.pivot = new Vector2(0.5f, 1f);
                bubbleRect.anchoredPosition = new Vector2(bottom.x, bottom.y - 18f);
            }
            // 水平夹在屏幕内
            float halfW = overlayRect.rect.width * 0.5f;
            float x = Mathf.Clamp(bubbleRect.anchoredPosition.x,
                -halfW + bubbleRect.rect.width * 0.5f + 20f,
                halfW - bubbleRect.rect.width * 0.5f - 20f);
            bubbleRect.anchoredPosition = new Vector2(x, bubbleRect.anchoredPosition.y);
        }
    }

    private void ApplyFrameRect(RectTransform fr, RectTransform target)
    {
        if (target == null) return;
        Vector3[] wc = new Vector3[4];
        target.GetWorldCorners(wc);
        Vector2 l0 = WorldToOverlayLocal(wc[0]);
        Vector2 l2 = WorldToOverlayLocal(wc[2]);
        Vector2 pad = new Vector2(highlightPadding, highlightPadding);
        fr.anchoredPosition = Vector2.Min(l0, l2) - pad;
        fr.sizeDelta = Vector2.Max(l0, l2) - Vector2.Min(l0, l2) + pad * 2f;
    }

    private void HideHint()
    {
        foreach (Image f in frames)
        {
            if (f != null) Destroy(f.gameObject);
        }
        frames.Clear();
        frameGts.Clear();
        if (bubbleRect != null) { Destroy(bubbleRect.gameObject); bubbleRect = null; }
        bubbleText = null;
    }

    // ================================================================
    // 阶段推进钩子(进下一阶段前会统一移除)
    // ================================================================
    private void HookStage(GameObject go, UnityAction action)
    {
        if (go == null || action == null) return;
        Button btn = EnsureButton(go);
        if (btn == null) return;
        foreach (StageHook h in stageHooks)
        {
            if (h.btn == btn && h.action == action) return; // 幂等，防止重复绑定
        }
        btn.onClick.AddListener(action);
        stageHooks.Add(new StageHook { btn = btn, action = action });
    }

    private void ClearStageHooks()
    {
        foreach (StageHook h in stageHooks)
        {
            if (h.btn != null) h.btn.onClick.RemoveListener(h.action);
        }
        stageHooks.Clear();
    }

    private Button EnsureButton(GameObject go)
    {
        if (go == null) return null;
        Graphic g = go.GetComponent<Graphic>();
        if (g == null) g = go.GetComponentInChildren<Graphic>();
        if (g == null)
        {
            Debug.LogWarning($"[Tutorial] {go.name} 上找不到Graphic，无法绑定点击");
            return null;
        }
        g.raycastTarget = true;
        Button btn = g.GetComponent<Button>();
        if (btn == null) btn = g.gameObject.AddComponent<Button>();
        return btn;
    }

    // ================================================================
    // 持久挂钩：印章/电话(幂等，引导结束依然生效)
    // ================================================================
    private void TryBindPersistentHooks()
    {
        // 用ResolveTarget而不是直接读target：印章/电话可能在弹窗里，进入阶段时才第一次解析
        BindPersistent(ResolveTarget(stamp1), () => OnStampClicked(1));
        BindPersistent(ResolveTarget(stamp2), () => OnStampClicked(2));
        BindPersistent(ResolveTarget(phone), OnPhoneClicked);
    }

    private void BindPersistent(GameObject target, UnityAction action)
    {
        if (target == null || hookedTargets.Contains(target)) return;
        Button b = EnsureButton(target);
        if (b == null) return;
        b.onClick.AddListener(action);
        hookedTargets.Add(target);
    }

    /// <summary>外部弹窗实例化后调用：在该物体树下查找印章并挂钩</summary>
    public void BindStampsIn(GameObject root)
    {
        if (root == null) return;
        BindPersistent(FindDeepChild(root.transform, stamp1.findName), () => OnStampClicked(1));
        BindPersistent(FindDeepChild(root.transform, stamp2.findName), () => OnStampClicked(2));
    }

    private GameObject FindDeepChild(Transform root, string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (string.Equals(child.name, name, StringComparison.OrdinalIgnoreCase)) return child.gameObject;
        }
        return null;
    }

    // ================================================================
    // 印章/电话交互
    // ================================================================
    public void OnStampClicked(int stampId)
    {
        GuideTarget gt = stampId == 1 ? stamp1 : stamp2;
        GameObject stamp = ResolveTarget(gt);
        if (stamp != null) StartCoroutine(StampPunch(stamp));

        bool correct = stampId == 1; // 判定：Btn_Stamp1=通过(正确) Btn_Stamp2=不通过，接入真实审批数据后替换

        if (stage == Stage.Stamps)
        {
            NotifyApprovalResult(correct);
            EnterStage(Stage.Phone);
        }
        else if (stage == Stage.FreePlay)
        {
            NotifyApprovalResult(correct);
            NpcLeave();
        }
    }

    public void OnPhoneClicked()
    {
        if (stage == Stage.Phone)
        {
            NpcLeave();
            EnterStage(Stage.Screen);
        }
        else if (stage == Stage.FreePlay)
        {
            NotifyApprovalResult(false); // 扣留记为未通过，接入真实判定后替换
            NpcLeave();
        }
    }

    private IEnumerator StampPunch(GameObject stamp)
    {
        Transform tr = stamp.transform;
        Vector3 orig = tr.localScale;
        float t = 0f;
        while (t < 0.25f)
        {
            t += Time.deltaTime;
            float k = t / 0.25f;
            tr.localScale = orig * (1.6f - 0.6f * k);
            yield return null;
        }
        tr.localScale = orig;
    }

    // ================================================================
    // NPC离场 与 结算
    // ================================================================
    public void NpcLeave()
    {
        GameObject npc = ResolveTarget(npcGuest);
        if (npc == null || !npc.activeSelf)
        {
            NotifyNpcLeft();
            return;
        }
        if (leaveCoroutine != null) StopCoroutine(leaveCoroutine);
        leaveCoroutine = StartCoroutine(NpcLeaveRoutine(npc));
    }

    private IEnumerator NpcLeaveRoutine(GameObject npc)
    {
        CanvasGroup cg = npc.GetComponent<CanvasGroup>();
        if (cg == null) cg = npc.AddComponent<CanvasGroup>();
        Vector3 startScale = npc.transform.localScale;
        float t = 0f;
        while (t < leaveAnimDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / leaveAnimDuration);
            cg.alpha = 1f - k;
            npc.transform.localScale = startScale * (1f - 0.3f * k);
            yield return null;
        }
        npc.SetActive(false);
        cg.alpha = 1f;
        npc.transform.localScale = startScale;
        NotifyNpcLeft();
    }

    public void NotifyNpcLeft()
    {
        completedCount++;
        Debug.Log($"[Tutorial] NPC离场 {completedCount}/{totalNpcCount}");
        if (completedCount >= totalNpcCount && !settlementShown)
        {
            settlementShown = true;
            ShowSettlement();
        }
    }

    public void NotifyApprovalResult(bool correct)
    {
        if (correct) correctCount++;
        else wrongCount++;
    }

    /// <summary>腐化溢出时外部调用，计入结算</summary>
    public void NotifyCorruptionOverflow()
    {
        corruptionCount++;
    }

    private void ShowSettlement()
    {
        if (settlementRoot != null) Destroy(settlementRoot);

        settlementRoot = new GameObject("TutorialSettlement", typeof(RectTransform), typeof(Image), typeof(Button));
        settlementRoot.transform.SetParent(overlayRect, false);
        RectTransform sr = settlementRoot.GetComponent<RectTransform>();
        sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
        sr.offsetMin = Vector2.zero; sr.offsetMax = Vector2.zero;
        Image dim = settlementRoot.GetComponent<Image>();
        dim.color = new Color(0f, 0f, 0f, 0.6f);
        settlementRoot.GetComponent<Button>().onClick.AddListener(CloseSettlement);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(sr, false);
        Image panelImg = panel.GetComponent<Image>();
        panelImg.sprite = BubbleSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.raycastTarget = false;
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 0.5f); pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(680f, 460f);

        // 文本直接挂面板(不要中间容器，否则宽度为0会竖排)
        Text title = CreateText("【今日当铺结算】", pr, 42, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -44f);
        tr.sizeDelta = new Vector2(0f, 60f);

        string stats = $"今日接待：{completedCount} 位\n通过：{correctCount} 位\n未通过：{wrongCount} 位";
        if (corruptionCount > 0) stats += $"\n腐化溢出：{corruptionCount} 次";
        Text st = CreateText(stats, pr, 30, textColor, TextAnchor.MiddleCenter, FontStyle.Normal);
        RectTransform sr2 = st.rectTransform;
        sr2.anchorMin = new Vector2(0f, 0.25f); sr2.anchorMax = new Vector2(1f, 0.8f);
        sr2.offsetMin = Vector2.zero; sr2.offsetMax = Vector2.zero;

        Text hint = CreateText("点击任意位置继续营业", pr, 22, new Color(0.45f, 0.42f, 0.38f), TextAnchor.MiddleCenter, FontStyle.Normal);
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.anchoredPosition = new Vector2(0f, 22f);
        hr.sizeDelta = new Vector2(0f, 36f);
    }

    public void CloseSettlement()
    {
        if (settlementRoot != null) Destroy(settlementRoot);
        settlementRoot = null;
    }

    // ================================================================
    // 引导结束
    // ================================================================
    private void FinishGuide()
    {
        stage = Stage.FreePlay;
        stageOnNext = null;
        HideHint();
        ClearStageHooks();
        TryBindPersistentHooks();
        Debug.Log("[Tutorial] 基础引导结束，进入自由审批");
    }

    // ================================================================
    // 资源：圆角贴图 / 字体 / 文本
    // ================================================================
    private Sprite BubbleSprite
    {
        get
        {
            if (bubbleSpriteOverride != null) return bubbleSpriteOverride;
            if (fillSprite == null) fillSprite = MakeRoundedSprite(false);
            return fillSprite;
        }
    }

    private Sprite BorderSprite
    {
        get
        {
            if (borderSprite == null) borderSprite = MakeRoundedSprite(true);
            return borderSprite;
        }
    }

    /// <summary>SDF生成圆角矩形贴图：实底 / 描边，9-sliced</summary>
    private Sprite MakeRoundedSprite(bool isBorder)
    {
        const int size = 96;
        const float radius = 30f;
        const float halfBorder = 4f; // 描边半宽

        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.name = isBorder ? "TutorialBorder" : "TutorialFill";

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f - size * 0.5f;
                float py = y + 0.5f - size * 0.5f;
                float qx = Mathf.Abs(px) - (size * 0.5f - radius);
                float qy = Mathf.Abs(py) - (size * 0.5f - radius);
                float dist = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude
                           + Mathf.Min(Mathf.Max(qx, qy), 0f);
                float a;
                if (isBorder)
                {
                    a = 1f - Mathf.SmoothStep(-1f, 1f, Mathf.Abs(dist) - halfBorder);
                }
                else
                {
                    a = 1f - Mathf.SmoothStep(-1f, 1f, dist);
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }

    private Font ResolveFont()
    {
        if (fontOverride != null) return fontOverride;
        Font f = null;
        try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
        if (f == null) { try { f = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }
        if (f == null)
        {
            foreach (Text t in FindObjectsOfType<Text>())
            {
                if (t != null && t.font != null) { f = t.font; break; }
            }
        }
        if (f == null) Debug.LogWarning("[Tutorial] 没有可用字体，文本将不显示");
        return f;
    }

    private Text CreateText(string content, Transform parent, int size, Color color, TextAnchor anchor, FontStyle style)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Text t = go.AddComponent<Text>();
        t.font = ResolveFont();
        t.fontSize = size;
        t.color = color;
        t.alignment = anchor;
        t.fontStyle = style;
        t.raycastTarget = false;
        t.supportRichText = true;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.text = content;
        t.rectTransform.anchorMin = Vector2.zero;
        t.rectTransform.anchorMax = Vector2.one;
        t.rectTransform.offsetMin = Vector2.zero;
        t.rectTransform.offsetMax = Vector2.zero;
        return t;
    }
}
