using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 新手引导管理器（自包含，挂在场景任意物体上）
/// 阶段0 引导NPC打字机对话 → 阶段1~8 发光提示框依次框选铃铛/邮箱/档案/文书/印章/电话/信息屏/时钟
/// 引导结束后进入自由审批；NPC处理完(剩余0)或18点下班时自动弹出结算弹窗。
/// 目标 = Inspector拖拽(优先) / 按 findName 自动查找(支持逗号分隔多个备选名，忽略大小写)。
/// 气泡尺寸与各阶段文案均为Inspector槽位。
/// </summary>
public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    /// <summary>当前是否处于自由审批模式（教程已结束）</summary>
    public bool IsFreePlay => stage == Stage.FreePlay;

    /// <summary>引导目标：手动拖拽优先，否则按名字查找(逗号分隔备选名)</summary>
    [Serializable]
    public class GuideTarget
    {
        [Tooltip("手动拖拽赋值(优先)")]
        public GameObject target;
        [Tooltip("target为空时按名字查找；多个备选名用英文逗号分隔，命中任意一个当前激活的即可")]
        public string findName;
    }

    private enum Stage { None, Welcome, ReviewRules, Info, Bell, MailBox, Profile, Document, Stamps, Phone, Screen, Clock, FreePlay }

    [Header("阶段0 引导对话")]
    public Sprite guideNpcSprite;                 // 引导NPC形象(可选)
    [Range(0.01f, 0.3f)] public float typingInterval = 0.06f; // 打字机速度(秒/字)
    [TextArea(2, 4)] public string welcomeText = "欢迎来到深空当铺。我会一步步教你这里的工作规则。";
    [TextArea(2, 4)] public string welcomeText2 = "跟着金色高亮提示操作，即可完成基础引导。";

    [Header("引导目标(能拖就拖；拖不了就把findName改成场景里的实际名字)")]
    public GuideTarget bell = new GuideTarget { findName = "Btn_Bell" };
    [Tooltip("审查规则按钮（铃铛之前教学）")]
    public GuideTarget btn7Rules = new GuideTarget { findName = "Btn_7" };
    [Tooltip("信息按钮（铃铛之前教学）")]
    public GuideTarget btn6Info = new GuideTarget { findName = "Btn_6" };
    [Tooltip("客人NPC，运行时由铃铛生成")]
    public GuideTarget npcGuest = new GuideTarget { findName = "NPC" };
    [Tooltip("新旧邮箱谁激活就高亮谁")]
    public GuideTarget mailbox = new GuideTarget { findName = "MailBox,MailBox2" };
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
    [TextArea(2, 4)] public string btn7RulesText = "请查看今日的审查规则";
    [TextArea(2, 4)] public string btn6InfoText = "可以查看信息";
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
    [Tooltip("退场动画时长(秒)")]
    public float leaveAnimDuration = 0.6f;
    [Tooltip("入场动画时长(秒)")]
    public float enterAnimDuration = 0.6f;
    public int totalNpcCount = 8;
    [Tooltip("等待高亮目标出现的超时(秒)。目标在场景中不存在时自动跳过该阶段，防止教程卡死")]
    public float missingTargetTimeout = 5f;

    [Header("结算面板外观")]
    [Tooltip("结算面板背景图，留空用代码生成的圆角白底")]
    public Sprite settlementBgSprite;
    [Tooltip("结算面板图标(预留图片槽位)，显示在标题上方")]
    public Sprite settlementIconSprite;
    [Tooltip("结算面板字体，留空用上方 fontOverride")]
    public Font settlementFont;
    [Tooltip("黑屏过渡总时长(秒)")]
    public float dayTransitionDuration = 2f;

    [Header("盖章视觉")]
    [Tooltip("盖在通关文书上的【通过】印章 Image（预制体上那两张里的红章）——留空则自动从 Btn_File3 子物体里找")]
    public Image sealImagePass;
    [Tooltip("盖在通关文书上的【不通过】印章 Image（预制体上那两张里的灰章）——留空则自动从 Btn_File3 子物体里找")]
    public Image sealImageReject;

    [Header("印章在弹窗上的位置调整")]
    [Tooltip("印章边长 = 弹窗宽度 × 此比例")]
    [Range(0.05f, 1f)] public float sealSizeRatio = 0.25f;
    [Tooltip("不使用自定义坐标时，印章距弹窗右下角的内边距(像素)")]
    public float sealPadding = 12f;
    [Tooltip("勾选后使用下面的自定义坐标，忽略内边距自动计算")]
    public bool useCustomSealPosition = false;
    [Tooltip("印章在通关文书弹窗上的自定义坐标(锚点=右下角, 单位像素)，X右正 Y上正")]
    public Vector2 sealAnchoredPosition = new Vector2(0f, 0f);

    [Header("盖章门槛")]
    [Tooltip("勾选后：必须先打开通关文书(Btn_File3)弹出的弹窗，才能点击印章盖章")]
    public bool requireFile3OpenToStamp = true;
    [Tooltip("没打开通关文书就点印章时，屏幕下方弹出的提示文字")]
    public string stampNeedFile3Hint = "请先打开通关文书，再盖章";

    [Header("盖章确认弹窗 —— 文本(可改)")]
    [Tooltip("弹窗标题")]
    public string stampConfirmTitle = "是否盖章？";
    [Tooltip("弹窗正文提示")]
    [TextArea(2, 4)] public string stampConfirmContent = "请确认本次审批结果";
    [Tooltip("【通过】按钮文字")]
    public string stampConfirmPassText = "通过";
    [Tooltip("【不通过】按钮文字")]
    public string stampConfirmRejectText = "不通过";

    [Header("盖章确认弹窗 —— 基础设置(可改)")]
    [Tooltip("弹窗背景图，留空用代码生成的圆角白底")]
    public Sprite stampConfirmBgSprite;
    [Tooltip("弹窗字体，留空用上方的 fontOverride")]
    public Font stampConfirmFont;
    [Tooltip("弹窗尺寸(像素)")]
    public Vector2 stampConfirmPanelSize = new Vector2(560f, 320f);
    [Tooltip("全屏遮罩颜色(越黑越遮挡背景)")]
    public Color stampConfirmDimmerColor = new Color(0f, 0f, 0f, 0.55f);
    [Tooltip("标题字号")]
    public int stampConfirmTitleFontSize = 38;
    [Tooltip("正文字号")]
    public int stampConfirmContentFontSize = 28;
    [Tooltip("两个按钮的尺寸(像素)")]
    public Vector2 stampConfirmButtonSize = new Vector2(170f, 66f);
    [Tooltip("按钮文字字号")]
    public int stampConfirmButtonFontSize = 30;
    [Tooltip("按钮底色")]
    public Color stampConfirmButtonColor = new Color(0.72f, 0.24f, 0.20f, 1f);
    [Tooltip("按钮文字颜色")]
    public Color stampConfirmButtonTextColor = Color.white;

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
    private Coroutine enterCoroutine;
    private GameObject leavingNpc;                 // 正在播退场动画的NPC
    private Vector3 npcBaseScale = Vector3.one;    // NPC原始缩放(第一次入场前记录)
    private bool npcBaseScaleSaved;
    private bool npcOnStage;                       // NPC是否还站在场上(未退场)
    private GameObject settlementRoot;
    private bool settlementShown;
    private int completedCount, correctCount, wrongCount, corruptionCount;
    private bool npcLocked;                        // 点过 mailbox 后锁 NPC，防止跳过审批
    private bool approvalProcessing;               // 审批链式协程正在跑，防止重复触发
    private Sprite builtinWhiteSprite;             // 内置白精灵缓存
    private GameObject stampConfirmRoot;           // "是否盖章"确认弹窗实例
    private int stampConfirmChoice;                // 确认结果：0=未选 1=通过 2=不通过 -1=作废

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
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnWorkEnd += OnWorkEndSettlement; // 18点下班自动结算
            GameTimeManager.Instance.OnNewDay += OnNewDayReset;        // 新的一天重置统计
        }
        StartCoroutine(EnterWelcomeNextFrame());
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance == null) return;
        GameTimeManager.Instance.OnWorkEnd -= OnWorkEndSettlement;
        GameTimeManager.Instance.OnNewDay -= OnNewDayReset;
    }

    private IEnumerator EnterWelcomeNextFrame()
    {
        yield return null; // 等一帧让场景UI先初始化
        EnterStage(Stage.Welcome);
    }

    void Update()
    {
        // NPC全部处理完(剩余0且场上无NPC) → 自动弹结算
        if (!settlementShown && stage == Stage.FreePlay
            && NPCDailyManager.Instance != null && NPCDailyManager.Instance.todayRemain <= 0)
        {
            GameObject npc = CurrentNpc();
            if (npc == null || !npc.activeInHierarchy)
            {
                settlementShown = true;
                ShowSettlement();
            }
        }

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
    /// <summary>白 sprite：动态创建 1x1 白贴图，避免依赖 Unity 版本相关的内置资源路径</summary>
    private Sprite GetBuiltinWhiteSprite()
    {
        if (builtinWhiteSprite != null) return builtinWhiteSprite;

        // 直接用代码生成一张白色贴图：Unity 各版本内置资源路径不一致，
        // GetBuiltinResource 找不到时会刷错误日志，所以不再尝试内置路径
        var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        var pixels = new Color[16];
        for (int i = 0; i < 16; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();
        builtinWhiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return builtinWhiteSprite;
    }

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
            EnterStage(Stage.ReviewRules);
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
            case Stage.ReviewRules:
                ShowStage(btn7Rules, btn7RulesText, () => EnterStage(Stage.Phone)); // 点开Btn_7弹窗后直接进入电话教程
                break;
            case Stage.Info:
                ShowStage(btn6Info, btn6InfoText, () => EnterStage(Stage.Bell));
                break;
            case Stage.Bell:
                ShowStage(bell, bellText, () => EnterStage(Stage.MailBox));
                break;
            case Stage.MailBox:
                // 新旧邮箱谁激活就高亮谁，指示立即出现
                ShowStage(mailbox, mailBoxText, () => EnterStage(Stage.Profile));
                break;
            case Stage.Profile:
                ShowStage(profile, profileText, () => EnterStage(Stage.Document));
                break;
            case Stage.Document:
                ShowStage(document, documentText, () => EnterStage(Stage.Stamps));
                break;
            case Stage.Stamps:
                ShowStage(new GuideTarget[] { stamp1, stamp2 }, stampsText, null, TryBindPersistentHooks);
                break;
            case Stage.Phone:
                // 电话在Btn_7弹窗内，点击推进由PopupTabs绑定的OnPhoneClicked处理（避免重复绑定）
                ShowStage(phone, phoneText, null);
                break;
            case Stage.Screen:
                ShowStage(screen, screenText, FinishGuide); // 点信息屏 直接结束教程进自由审批
                break;
            case Stage.Clock:
                // 兜底：如果不小心还是走进 Clock，显示时钟提示后 1.5 秒自动 FinishGuide
                StartCoroutine(ClockAutoFinish());
                break;
        }
    }

    private IEnumerator ClockAutoFinish()
    {
        var clock = FindObjectOfType<NPCBellLogic>() != null
            ? GameObject.Find("clock") : GameObject.Find("clock");
        ShowStage(new GuideTarget { findName = "clock" }, "教程即将结束...", null);
        yield return new WaitForSeconds(1.5f);
        if (stage == Stage.Clock) FinishGuide();
    }

    private void ShowStage(GuideTarget gt, string text, UnityAction onNext, Action afterReady = null)
    {
        StartCoroutine(ShowStageRoutine(new GuideTarget[] { gt }, text, onNext, afterReady));
    }

    private void ShowStage(GuideTarget[] gts, string text, UnityAction onNext, Action afterReady = null)
    {
        StartCoroutine(ShowStageRoutine(gts, text, onNext, afterReady));
    }

    /// <summary>
    /// 等待目标激活后显示提示。目标未出现时每0.1秒轮询，
    /// GameObject.Find未命中时定期做忽略大小写的全量扫描兜底。
    /// 超过 missingTargetTimeout 秒仍找不到目标时打印错误并自动跳过本阶段，避免教程卡死。
    /// </summary>
    private IEnumerator ShowStageRoutine(GuideTarget[] gts, string text, UnityAction onNext, Action afterReady)
    {
        Stage myStage = stage;
        stageOnNext = onNext;
        bool warned = false;
        int tick = 0;
        float waitTime = 0f;

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

            // 超时保护：目标大概率不存在于场景中，跳过本阶段避免教程永久卡死
            waitTime += 0.1f;
            if (waitTime >= missingTargetTimeout)
            {
                string names = "";
                foreach (GuideTarget gt in gts) names += gt.findName + " ";
                Debug.LogError($"[Tutorial] 等待超时，目标不存在：{names.Trim()}。自动跳过本阶段。" +
                               "请检查场景物体名或在 Inspector 中修改 findName。");
                if (stage == myStage) onNext?.Invoke();
                yield break;
            }

            if (!warned)
            {
                warned = true;
                Debug.Log($"[Tutorial] 等待目标出现：{gts[0].findName} (目标激活后自动继续)");
            }
            yield return new WaitForSeconds(0.1f);
        }

        if (stage != myStage) yield break;

        ShowHint(gts, text);

        if (onNext != null)
        {
            foreach (GuideTarget gt in gts)
            {
                if (gt.target != null) HookStage(gt.target, onNext);
            }
        }
        if (afterReady != null) afterReady();
    }

    private void ShowHint(GuideTarget[] gts, string text)
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
        bubbleText = CreateText(text, bubbleRect, bubbleFontSize, textColor, TextAnchor.MiddleLeft, FontStyle.Normal);
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
    public void TryBindPersistentHooks()
    {
        // 用ResolveTarget而不是直接读target：印章可能在弹窗里，进入阶段时才第一次解析
        // 注意：电话不在这里绑——电话在Btn_7弹窗内，由PopupTabs每次实例化时绑定，绑两次会触发两次
        BindPersistent(ResolveTarget(stamp1), () => OnStampClicked(1));
        BindPersistent(ResolveTarget(stamp2), () => OnStampClicked(2));
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
        Debug.Log($"[Tutorial] OnStampClicked stampId={stampId} stage={stage} approvalProcessing={approvalProcessing}");
        if (approvalProcessing) { Debug.LogWarning("[Tutorial] OnStampClicked 被锁"); return; }

        // 盖章门槛：必须先打开通关文书(Btn_File3)的弹窗
        if (requireFile3OpenToStamp && !IsFile3PopupOpen())
        {
            Debug.LogWarning("[Tutorial] 通关文书(Btn_File3)未打开，禁止盖章");
            StartCoroutine(ToastRoutine(stampNeedFile3Hint));
            return;
        }

        // Fallback：如果还卡在教程阶段（Screen/Clock）但玩家已经能点到 Stamp，
        // 说明教程对话已经走完了，强制收尾进 FreePlay
        if (stage != Stage.Stamps && stage != Stage.FreePlay)
        {
            Debug.LogWarning($"[Tutorial] stage={stage} 但 Stamp 被点了，强制 FinishGuide → FreePlay");
            FinishGuide();
        }

        GuideTarget gt = stampId == 1 ? stamp1 : stamp2;
        GameObject stamp = ResolveTarget(gt);

        approvalProcessing = true;
        if (stage == Stage.Stamps)
        {
            StartCoroutine(StampApprovalChain_Tutorial(stamp));
        }
        else if (stage == Stage.FreePlay)
        {
            StartCoroutine(StampApprovalChain(stamp));
        }
        else
        {
            Debug.LogWarning($"[Tutorial] OnStampClicked 但当前既不是 Stamps 也不是 FreePlay 阶段！stage={stage}");
            approvalProcessing = false;
        }
    }

    /// <summary>教程阶段盖章：印章缩放 → 确认弹窗(通过/不通过) → 盖到 Btn_File3 → 结束教程</summary>
    private IEnumerator StampApprovalChain_Tutorial(GameObject stampObj)
    {
        if (stampObj != null) yield return StartCoroutine(StampPunch(stampObj));

        // 弹"是否盖章"确认弹窗，等玩家选【通过】/【不通过】
        yield return StartCoroutine(ShowStampConfirmPopup());
        int finalStamp = stampConfirmChoice;
        if (finalStamp != 1 && finalStamp != 2) { approvalProcessing = false; yield break; }

        yield return StartCoroutine(ShowDocumentStampPopup(finalStamp));

        NotifyApprovalResult(finalStamp == 1);
        approvalProcessing = false;
        FinishGuide(); // 电话已提前到Btn_7之后，盖章是教程最后一步
    }

    public void OnPhoneClicked()
    {
        Debug.Log($"[Tutorial] OnPhoneClicked stage={stage} approvalProcessing={approvalProcessing}");
        if (approvalProcessing) { Debug.LogWarning("[Tutorial] OnPhoneClicked 被锁"); return; }

        // 教程前段（Bell/MailBox等阶段）乱点弹窗内电话：忽略，不打断教程
        if (stage != Stage.Phone && stage != Stage.FreePlay)
        {
            Debug.Log($"[Tutorial] stage={stage} 电话被点击，忽略");
            return;
        }

        if (stage == Stage.Phone)
        {
            approvalProcessing = true;
            StartCoroutine(PhoneApprovalChain_Tutorial());
        }
        else if (stage == Stage.FreePlay)
        {
            // 自由审批：点击电话不再让NPC离开，NPC继续留场，由印章完成审批
            Debug.Log("[Tutorial] 自由审批点击电话：NPC不离场");
        }
    }

    /// <summary>教程阶段电话（Btn_7后）：点击电话后推进信息按钮教程（弹窗保持打开，教程继续）</summary>
    private IEnumerator PhoneApprovalChain_Tutorial()
    {
        yield return null;
        approvalProcessing = false;
        EnterStage(Stage.Info);
    }

    /// <summary>自由审批：印章点击链式——印章缩放 → 确认弹窗 → 通关文书弹窗+盖章 → NPC退场 → 关弹窗 → 等玩家再次手动摇铃</summary>
    private IEnumerator StampApprovalChain(GameObject stampObj)
    {
        // 1. 印章按钮本身缩放动画
        if (stampObj != null) yield return StartCoroutine(StampPunch(stampObj));

        // 2. 弹"是否盖章"确认弹窗，等玩家选【通过】/【不通过】
        yield return StartCoroutine(ShowStampConfirmPopup());
        int finalStamp = stampConfirmChoice;
        if (finalStamp != 1 && finalStamp != 2) { approvalProcessing = false; yield break; }

        // 3. 弹通关文书 + 盖章图片
        yield return StartCoroutine(ShowDocumentStampPopup(finalStamp));

        // 4. NPC 退场
        NpcLeave();
        while (leaveCoroutine != null) yield return null;

        // 5. 关掉所有弹窗（NPC 走了档案/文书都该关）
        RoomInteract.CloseAllPopups();

        // 6. 通知结果 + 收尾（不自动叫下一位，由玩家再次摇铃）
        NotifyApprovalResult(finalStamp == 1);
        yield return FinishNpcAndRestore();
    }

    /// <summary>NPC退场收尾：解锁NPC、释放审批锁、恢复桌面与铃铛，等待玩家再次手动摇铃</summary>
    private IEnumerator FinishNpcAndRestore()
    {
        yield return null; // 等一帧让 NpcLeaveRoutine 彻底收尾
        UnlockNpc();
        approvalProcessing = false;
        NPCBellLogic bell = FindObjectOfType<NPCBellLogic>();
        if (bell != null) bell.OnNpcLeft();
    }

    /// <summary>把盖章图挂到 PopupPanel 子物体上，锚点右下角；大小/位置由 Inspector 槽位控制</summary>
    private void AttachSealToPanel(RectTransform sealRT, RectTransform panelRT)
    {
        sealRT.SetParent(panelRT, false);
        sealRT.anchorMin = sealRT.anchorMax = new Vector2(1, 0); // 右下角
        sealRT.pivot = new Vector2(0.5f, 0.5f);
        float s = panelRT.rect.width * sealSizeRatio;
        sealRT.sizeDelta = new Vector2(s, s);
        // 坐标：勾选自定义就用槽位值，否则按"内边距贴右下角"自动算
        // 锚点在右下角，X 取负值 = 向左留出内边距
        sealRT.anchoredPosition = useCustomSealPosition
            ? sealAnchoredPosition
            : new Vector2(-(s * 0.5f + sealPadding), s * 0.5f + sealPadding);
        sealRT.localScale = Vector3.one;
        Image sealImg = sealRT.GetComponent<Image>();
        if (sealImg != null) sealImg.raycastTarget = false;
    }

    /// <summary>Clone 一张 Image，只保留 Image + RectTransform，绝不 SetParent 原图</summary>
    private Image CloneImage(Image source, string cloneName)
    {
        GameObject cloneGO = Instantiate(source.gameObject);
        cloneGO.name = cloneName;
        // 移除可能的 Button/Outline 等多余组件，只留 Image + RectTransform + CanvasRenderer
        var extras = cloneGO.GetComponents<Component>();
        foreach (var c in extras)
        {
            if (c is Transform || c is RectTransform || c is CanvasRenderer || c is Image) continue;
            Destroy(c);
        }
        var img = cloneGO.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
        return img;
    }

    private void EnsureSealImages()
    {
        if (sealImagePass != null && sealImageReject != null) return;

        // 候选关键词（大小写不敏感）
        string[] passKws = { "SealPass", "PassSeal", "Pass", "通过", "Seal_Pass", "StampPass" };
        string[] rejectKws = { "SealReject", "RejectSeal", "Reject", "不通过", "Seal_Reject", "StampReject", "Fail", "Refuse" };

        // 1. 扫 Btn_File3 子物体
        GameObject file3 = FindObjectOfType<NPCBellLogic>() != null
            ? FindObjectOfType<NPCBellLogic>().Btn_File3
            : GameObject.Find("Btn_File3");
        if (file3 != null)
        {
            Image[] subImgs = file3.GetComponentsInChildren<Image>(true);
            Debug.Log($"[Tutorial] Btn_File3 下共 {subImgs.Length} 个 Image 子物体");
            foreach (Image img in subImgs)
            {
                TryMatchSealImage(img, passKws, rejectKws);
            }
        }

        // 2. 还没找到 → 扫整个场景（Resources.FindObjectsOfTypeAll 查隐藏物体）
        if (sealImagePass == null || sealImageReject == null)
        {
            Image[] allImgs = Resources.FindObjectsOfTypeAll<Image>();
            foreach (Image img in allImgs)
            {
                if (!img.gameObject.scene.IsValid()) continue; // 排除 Project 窗口的预制体资源
                TryMatchSealImage(img, passKws, rejectKws);
                if (sealImagePass != null && sealImageReject != null) break;
            }
            if (sealImagePass != null || sealImageReject != null)
                Debug.Log("[Tutorial] 从全场景扫到盖章图");
        }

        if (sealImagePass == null && sealImageReject == null)
            Debug.LogWarning("[Tutorial] 全场景没扫到任何盖章图，将用动态白方块兜底");

        // 兜底：只找到一张就 Pass/Reject 共用
        if (sealImagePass == null && sealImageReject != null) sealImagePass = sealImageReject;
    }

    private void TryMatchSealImage(Image img, string[] passKws, string[] rejectKws)
    {
        string n = img.gameObject.name;

        // ===== 过滤大物体/重要 UI：绝不能把 Calendar/Clock/NPC 当盖章图 =====
        string[] blockKws = { "Calendar", "Clock", "NPC", "Background", "Bg", "Panel", "Frame", "MailBox", "Btn_", "Image", "Sprite", "Text", "Title", "Content" };
        foreach (var bw in blockKws)
        {
            if (n.IndexOf(bw, System.StringComparison.OrdinalIgnoreCase) >= 0) return;
        }
        // 体积过滤：盖章图不可能比 200px 大
        var rt = img.GetComponent<RectTransform>();
        if (rt != null && rt.rect.width > 200f) return;

        Color c = img.color;
        bool isRed = c.r > 0.6f && c.g < 0.4f && c.b < 0.4f;
        bool isGray = c.r < 0.7f && Mathf.Abs(c.r - c.g) < 0.1f && Mathf.Abs(c.g - c.b) < 0.1f;

        if (sealImagePass == null)
        {
            foreach (var kw in passKws)
            {
                if (n.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sealImagePass = img;
                    Debug.Log($"[Tutorial] 匹配到 sealImagePass: {n}");
                    return;
                }
            }
        }

        if (sealImageReject == null)
        {
            foreach (var kw in rejectKws)
            {
                if (n.IndexOf(kw, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sealImageReject = img;
                    Debug.Log($"[Tutorial] 匹配到 sealImageReject: {n}");
                    return;
                }
            }
        }

        // 纯颜色兜底：名字没匹配，但颜色对也可以（但必须过上面的 blockKws 和体积过滤）
        if (sealImagePass == null && isRed)
        {
            sealImagePass = img;
            Debug.Log($"[Tutorial] 颜色兜底 sealImagePass(红): {n}");
        }
        else if (sealImageReject == null && isGray)
        {
            sealImageReject = img;
            Debug.Log($"[Tutorial] 颜色兜底 sealImageReject(灰): {n}");
        }
    }

    /// <summary>拿到 File3 弹出来的 PopupPanel（popupSource="File3"），忽略 File2 的档案弹窗</summary>
    private RectTransform FindActivePopupPanel()
    {
        PopupPanel[] panels = Resources.FindObjectsOfTypeAll<PopupPanel>();
        foreach (var p in panels)
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            if (string.Equals(p.popupSource, "File3", System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"[Tutorial] 找到 File3 弹窗: {p.gameObject.name}");
                return p.GetComponent<RectTransform>();
            }
        }
        // 兜底：如果 File3 没标记（玩家没改 RoomInteract.popupSource），找最后一个激活的
        foreach (var p in panels)
        {
            if (p != null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy)
            {
                Debug.LogWarning($"[Tutorial] File3 来源没标记，兜底用最后一个激活的 PopupPanel: {p.gameObject.name}");
                return p.GetComponent<RectTransform>();
            }
        }
        Debug.LogWarning("[Tutorial] 没找到激活的 PopupPanel");
        return null;
    }

    /// <summary>在 Btn_File3 弹出的 PopupPanel 右下角盖印章，1.5 秒后自动隐藏。
    /// 盖章图挂到 PopupPanel 子物体上，弹窗怎么拖都跟着。</summary>
    private IEnumerator ShowDocumentStampPopup(int stampId)
    {
        Debug.Log($"[Tutorial] ShowDocumentStampPopup stampId={stampId}");

        EnsureSealImages();

        // 找到当前激活的 PopupPanel 弹窗实例
        RectTransform popupRT = FindActivePopupPanel();
        if (popupRT == null)
        {
            Debug.LogWarning("[Tutorial] 没找到 PopupPanel，盖章失败");
            yield break;
        }

        Image sealPass = sealImagePass;
        Image sealReject = sealImageReject;
        RectTransform sealRT = null;

        if (sealPass != null || sealReject != null)
        {
            // === 绝对安全：Clone 一份盖章图来用，绝不 SetParent 原物体 ===
            Image clonedPass = sealPass != null ? CloneImage(sealPass, "SealPass_Clone") : null;
            Image clonedReject = sealReject != null ? CloneImage(sealReject, "SealReject_Clone") : null;

            if (clonedPass != null) clonedPass.gameObject.SetActive(false);
            if (clonedReject != null) clonedReject.gameObject.SetActive(false);

            // 把 Clone 挂到 PopupPanel 下、锚点设右下角
            if (clonedPass != null) AttachSealToPanel(clonedPass.rectTransform, popupRT);
            if (clonedReject != null) AttachSealToPanel(clonedReject.rectTransform, popupRT);

            if (stampId == 1)
            {
                if (clonedPass != null) { sealRT = clonedPass.rectTransform; clonedPass.gameObject.SetActive(true); }
                if (clonedReject != null) clonedReject.gameObject.SetActive(false);
            }
            else
            {
                if (clonedReject != null) { sealRT = clonedReject.rectTransform; clonedReject.gameObject.SetActive(true); }
                if (clonedPass != null) clonedPass.gameObject.SetActive(false);
            }
            if (sealRT == null)
            {
                Debug.LogWarning("[Tutorial] Clone 后盖章图还是 null！pass=" + (clonedPass != null) + " reject=" + (clonedReject != null));
                yield break;
            }
        }
        else
        {
            // 兜底：动态创建一张盖章图，直接挂 PopupPanel 下、锚点右下角
            Image seal;
            GameObject sealGO = new GameObject("SealOnPopup", typeof(RectTransform), typeof(Image));
            sealGO.transform.SetParent(popupRT, false);
            sealRT = sealGO.GetComponent<RectTransform>();
            sealRT.anchorMin = sealRT.anchorMax = new Vector2(1, 0); // 右下角
            sealRT.pivot = new Vector2(0.5f, 0.5f);
            float s = popupRT.rect.width * 0.25f;
            sealRT.sizeDelta = new Vector2(s, s);
            // X 取负值 = 从右边缘向左留出内边距
            sealRT.anchoredPosition = new Vector2(-(s * 0.5f + 12), s * 0.5f + 12);
            sealRT.localScale = Vector3.zero;
            seal = sealGO.GetComponent<Image>();
            seal.color = stampId == 1
                ? new Color(0.85f, 0.1f, 0.1f, 0.85f)
                : new Color(0.55f, 0.55f, 0.55f, 0.85f);
            seal.sprite = GetBuiltinWhiteSprite();
            seal.raycastTarget = false;

            GameObject labelGO = new GameObject("SealLabel", typeof(RectTransform));
            labelGO.transform.SetParent(sealRT, false);
            Text sealText = labelGO.AddComponent<Text>();
            sealText.alignment = TextAnchor.MiddleCenter;
            sealText.fontSize = 28;
            sealText.fontStyle = FontStyle.Bold;
            sealText.color = Color.white;
            sealText.text = stampId == 1 ? "✓ 通过" : "✗ 不通过";
            sealText.resizeTextForBestFit = true;
            sealText.resizeTextMinSize = 10;
            sealText.resizeTextMaxSize = 48;
            sealText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (sealText.font == null) sealText.font = fontOverride;
            RectTransform labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = Vector2.zero; labelRT.anchorMax = Vector2.one;
            labelRT.offsetMin = Vector2.zero; labelRT.offsetMax = Vector2.zero;
        }

        // 砸落 + 超冲回弹
        float t = 0f;
        while (t < 0.35f)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / 0.35f);
            float s = k < 0.7f ? (0.5f + k * 1.2f) : (1.34f - (k - 0.7f) * 1.13f);
            if (sealRT == null) yield break; // 弹窗被玩家提前关了
            sealRT.localScale = Vector3.one * s;
            yield return null;
        }
        if (sealRT == null) yield break;
        sealRT.localScale = Vector3.one;

        yield return new WaitForSeconds(1.5f);

        if (sealRT == null) yield break; // 弹窗被玩家提前关了
        sealRT.localScale = Vector3.zero; // 缩回去
        yield return new WaitForSeconds(0.15f);

        // Clone 版：什么都不做 —— Clone 的那份会随着 PopupPanel 关闭被 Destroy
    }

    // ================================================================
    // 盖章门槛 + "是否盖章"确认弹窗
    // ================================================================
    /// <summary>严格判断：通关文书(Btn_File3, popupSource="File3")的弹窗是否已经打开</summary>
    private bool IsFile3PopupOpen()
    {
        PopupPanel[] panels = Resources.FindObjectsOfTypeAll<PopupPanel>();
        foreach (var p in panels)
        {
            if (p == null || !p.gameObject.scene.IsValid() || !p.gameObject.activeInHierarchy) continue;
            if (string.Equals(p.popupSource, "File3", System.StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    /// <summary>弹出"是否盖章(通过/不通过)"确认弹窗，等玩家选择；结果写入 stampConfirmChoice(1=通过 2=不通过)</summary>
    private IEnumerator ShowStampConfirmPopup()
    {
        if (stampConfirmRoot != null) { Destroy(stampConfirmRoot); stampConfirmRoot = null; }
        stampConfirmChoice = 0;

        // 全屏遮罩(挡住背后所有点击)
        stampConfirmRoot = new GameObject("StampConfirm", typeof(RectTransform), typeof(Image));
        stampConfirmRoot.transform.SetParent(overlayRect, false);
        RectTransform cr = stampConfirmRoot.GetComponent<RectTransform>();
        cr.anchorMin = Vector2.zero; cr.anchorMax = Vector2.one;
        cr.offsetMin = Vector2.zero; cr.offsetMax = Vector2.zero;
        cr.SetAsLastSibling();
        Image dim = stampConfirmRoot.GetComponent<Image>();
        dim.color = stampConfirmDimmerColor;
        dim.raycastTarget = true;

        // 弹窗面板
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(cr, false);
        Image panelImg = panel.GetComponent<Image>();
        panelImg.sprite = stampConfirmBgSprite != null ? stampConfirmBgSprite : BubbleSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.raycastTarget = false;
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(Mathf.Max(200f, stampConfirmPanelSize.x), Mathf.Max(140f, stampConfirmPanelSize.y));

        Font cf = stampConfirmFont != null ? stampConfirmFont : ResolveFont();

        // 标题
        Text title = CreateText(stampConfirmTitle, pr, stampConfirmTitleFontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        title.font = cf;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, -18f);
        tr.sizeDelta = new Vector2(-32f, 56f);

        // 正文
        Text content = CreateText(stampConfirmContent, pr, stampConfirmContentFontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Normal);
        content.font = cf;
        RectTransform ctr = content.rectTransform;
        ctr.anchorMin = new Vector2(0f, 0.36f); ctr.anchorMax = new Vector2(1f, 0.7f);
        ctr.offsetMin = new Vector2(24f, 0f); ctr.offsetMax = new Vector2(-24f, 0f);

        // 两个按钮：左=通过(1) 右=不通过(2)
        float bw = Mathf.Max(60f, stampConfirmButtonSize.x);
        float bh = Mathf.Max(30f, stampConfirmButtonSize.y);
        float gap = 40f;
        CreateConfirmButton("Btn_Pass", stampConfirmPassText, pr, cf, new Vector2(-(bw + gap) * 0.5f, 48f), bw, bh, 1);
        CreateConfirmButton("Btn_Reject", stampConfirmRejectText, pr, cf, new Vector2((bw + gap) * 0.5f, 48f), bw, bh, 2);

        Debug.Log("[Tutorial] 盖章确认弹窗已弹出，等待玩家选择");

        // 等玩家选择（OnNewDayReset 会把 stampConfirmChoice 置 -1 强制解除等待）
        while (stampConfirmChoice == 0) yield return null;

        if (stampConfirmRoot != null) Destroy(stampConfirmRoot);
        stampConfirmRoot = null;
        Debug.Log($"[Tutorial] 盖章确认结果 choice={stampConfirmChoice}");
    }

    private void CreateConfirmButton(string name, string label, RectTransform parent, Font font, Vector2 pos, float w, float h, int choice)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.sprite = BubbleSprite; // 圆角白底（代码生成，不依赖内置资源）
        img.type = Image.Type.Sliced;
        img.color = stampConfirmButtonColor;
        img.raycastTarget = true;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(w, h);

        Text t = CreateText(label, rt, stampConfirmButtonFontSize, stampConfirmButtonTextColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        t.font = font;

        Button btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        int c = choice;
        btn.onClick.AddListener(() => { stampConfirmChoice = c; });
    }

    /// <summary>屏幕下方弹一条自动消失的提示（盖章门槛拦截等场景用）</summary>
    private IEnumerator ToastRoutine(string text)
    {
        if (string.IsNullOrEmpty(text)) yield break;

        GameObject b = new GameObject("TutorialToast", typeof(RectTransform), typeof(Image));
        b.transform.SetParent(overlayRect, false);
        Image img = b.GetComponent<Image>();
        img.sprite = BubbleSprite;
        img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        RectTransform rt = b.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 120f);
        rt.sizeDelta = new Vector2(Mathf.Max(240f, bubbleSize.x), Mathf.Max(60f, bubbleSize.y));
        rt.SetAsLastSibling();

        Text t = CreateText(text, rt, bubbleFontSize, textColor, TextAnchor.MiddleCenter, FontStyle.Normal);
        t.rectTransform.offsetMin = new Vector2(24f, 14f);
        t.rectTransform.offsetMax = new Vector2(-24f, -14f);

        yield return new WaitForSeconds(1.6f);
        if (b != null) Destroy(b);
    }

    /// <summary>世界坐标 → 指定 RectTransform 局部坐标</summary>
    private static Vector2 WorldToLocal(RectTransform parent, Vector3 worldPos)
    {
        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, worldPos, null, out local);
        return local;
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
    // NPC 入场 / 离场
    // ================================================================

    /// <summary>资料已打开时，NPC 不可再点(防止跳过审批直接离场)</summary>
    public bool IsNpcLocked => npcLocked;
    public void LockNpc() => npcLocked = true;
    public void UnlockNpc() => npcLocked = false;

    /// <summary>NPC入场：淡入+放大。前一位还站在场上时，先播他的退场再入场</summary>
    private GameObject CurrentNpc()
    {
        if (npcGuest.target == null)
        {
            NPCBellLogic bell = FindObjectOfType<NPCBellLogic>();
            if (bell != null && bell.npcObj != null) npcGuest.target = bell.npcObj;
        }
        if (npcGuest.target == null) npcGuest.target = ResolveTarget(npcGuest, true);
        return npcGuest.target;
    }

    public void NpcLeave()
    {
        GameObject npc = CurrentNpc();
        if (npc == null || !npc.activeSelf)
        {
            npcOnStage = false;
            NotifyNpcLeft();
            return;
        }
        if (leavingNpc == npc && leaveCoroutine != null) return; // 已在退场中，忽略重复触发
        CancelNpcLeave(); // 打断并还原上一个未完成的退场
        if (enterCoroutine != null) { StopCoroutine(enterCoroutine); enterCoroutine = null; }
        leavingNpc = npc;
        leaveCoroutine = StartCoroutine(NpcLeaveRoutine(npc));
    }

    /// <summary>NPC入场：淡入+放大。前一位还站在场上时，先播他的退场再入场</summary>
    public void NpcEnter()
    {
        GameObject npc = CurrentNpc();
        if (npc == null) return;
        if (enterCoroutine != null) { StopCoroutine(enterCoroutine); enterCoroutine = null; }
        enterCoroutine = StartCoroutine(NpcEnterRoutine(npc));
    }

    /// <summary>取消未完成的退场动画并还原NPC外观</summary>
    public void CancelNpcLeave()
    {
        if (leaveCoroutine != null) { StopCoroutine(leaveCoroutine); leaveCoroutine = null; }
        if (leavingNpc != null)
        {
            CanvasGroup cg = leavingNpc.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
            leavingNpc.transform.localScale = npcBaseScale;
            leavingNpc = null;
        }
    }

    private void SaveBaseScale(GameObject npc)
    {
        if (npcBaseScaleSaved) return;
        npcBaseScale = npc.transform.localScale;
        npcBaseScaleSaved = true;
    }

    private IEnumerator NpcEnterRoutine(GameObject npc)
    {
        CanvasGroup cg = npc.GetComponent<CanvasGroup>();
        if (cg == null) cg = npc.AddComponent<CanvasGroup>();
        SaveBaseScale(npc);

        // 前一位还站在场上且没在退场：先播他的退场
        if (npcOnStage && leaveCoroutine == null && npc.activeSelf)
        {
            leavingNpc = npc;
            leaveCoroutine = StartCoroutine(NpcLeaveRoutine(npc));
        }
        while (leaveCoroutine != null) yield return null; // 退场播完再入场

        if (npc == null) { enterCoroutine = null; yield break; } // NPC已被销毁(如跨天重置)，放弃入场

        npc.SetActive(true); // 退场协程结束时会隐藏，重新激活
        cg.alpha = 0f;
        float t = 0f;
        while (t < enterAnimDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / enterAnimDuration);
            if (npc == null) { enterCoroutine = null; yield break; } // 动画中途NPC被销毁
            cg.alpha = k;
            npc.transform.localScale = npcBaseScale * (0.7f + 0.3f * k);
            yield return null;
        }
        if (npc == null) { enterCoroutine = null; yield break; }
        cg.alpha = 1f;
        npc.transform.localScale = npcBaseScale;
        enterCoroutine = null;
        npcOnStage = true;
        npcLocked = false; // 新 NPC 入场后解锁，允许点击
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
            if (npc == null) // 动画中途NPC被销毁(如跨天重置)，静默收尾
            {
                leaveCoroutine = null;
                leavingNpc = null;
                npcOnStage = false;
                yield break;
            }
            cg.alpha = 1f - k;
            npc.transform.localScale = startScale * (1f - 0.3f * k);
            yield return null;
        }
        if (npc == null) // 播完瞬间被销毁，静默收尾
        {
            leaveCoroutine = null;
            leavingNpc = null;
            npcOnStage = false;
            yield break;
        }
        npc.SetActive(false);
        cg.alpha = 1f;
        npc.transform.localScale = npcBaseScaleSaved ? npcBaseScale : startScale;
        if (leavingNpc == npc) leavingNpc = null;
        leaveCoroutine = null;
        npcOnStage = false;
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

    /// <summary>18点下班：自动弹出结算</summary>
    private void OnWorkEndSettlement()
    {
        if (settlementShown) return;
        settlementShown = true;
        ShowSettlement();
    }

    /// <summary>新的一天：重置结算状态与统计，彻底重置 NPC 状态</summary>
    private void OnNewDayReset()
    {
        CloseSettlement();
        settlementShown = false;
        completedCount = correctCount = wrongCount = corruptionCount = 0;
        npcLocked = false;
        approvalProcessing = false;

        // 作废可能还挂着的盖章确认弹窗，避免旧协程一直等待
        if (stampConfirmRoot != null) { Destroy(stampConfirmRoot); stampConfirmRoot = null; }
        stampConfirmChoice = -1;

        // 彻底重置 NPC 状态（修第二天铃铛点不起的根因）
        npcOnStage = false;
        if (leaveCoroutine != null) { StopCoroutine(leaveCoroutine); leaveCoroutine = null; }
        if (enterCoroutine != null) { StopCoroutine(enterCoroutine); enterCoroutine = null; }
        leavingNpc = null;

        Debug.Log("[Tutorial] OnNewDayReset 完成，NPC状态已重置");
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
        settlementRoot.GetComponent<Button>().onClick.AddListener(OnSettlementClicked);

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(sr, false);
        Image panelImg = panel.GetComponent<Image>();
        // 背景图：优先用 Inspector 拖的，留空用代码生成的圆角白底
        panelImg.sprite = settlementBgSprite != null ? settlementBgSprite : BubbleSprite;
        panelImg.type = Image.Type.Sliced;
        panelImg.raycastTarget = false;
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = new Vector2(0.5f, 0.5f); pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(680f, 460f);

        // 结算字体：优先用 Inspector 拖的 settlementFont，留空用 fontOverride
        Font stFont = settlementFont != null ? settlementFont : ResolveFont();

        float titleY = -44f;

        // 图标（预留图片槽位）
        if (settlementIconSprite != null)
        {
            GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGO.transform.SetParent(pr, false);
            Image iconImg = iconGO.GetComponent<Image>();
            iconImg.sprite = settlementIconSprite;
            iconImg.preserveAspect = true;
            iconImg.raycastTarget = false;
            RectTransform iconRT = iconGO.GetComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.5f, 1f); iconRT.anchorMax = new Vector2(0.5f, 1f);
            iconRT.pivot = new Vector2(0.5f, 1f);
            iconRT.anchoredPosition = new Vector2(0f, -16f);
            iconRT.sizeDelta = new Vector2(72f, 72f);
            titleY = -100f; // 有图标时标题下移
        }

        // 文本直接挂面板(不要中间容器，否则宽度为0会竖排)
        Text title = CreateText("【今日当铺结算】", pr, 42, textColor, TextAnchor.MiddleCenter, FontStyle.Bold);
        title.font = stFont;
        RectTransform tr = title.rectTransform;
        tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f);
        tr.pivot = new Vector2(0.5f, 1f);
        tr.anchoredPosition = new Vector2(0f, titleY);
        tr.sizeDelta = new Vector2(0f, 60f);

        string stats = $"今日接待：{completedCount} 位\n通过：{correctCount} 位\n未通过：{wrongCount} 位";
        if (corruptionCount > 0) stats += $"\n腐化溢出：{corruptionCount} 次";
        Text st = CreateText(stats, pr, 30, textColor, TextAnchor.MiddleCenter, FontStyle.Normal);
        st.font = stFont;
        RectTransform sr2 = st.rectTransform;
        sr2.anchorMin = new Vector2(0f, 0.25f); sr2.anchorMax = new Vector2(1f, 0.8f);
        sr2.offsetMin = Vector2.zero; sr2.offsetMax = Vector2.zero;

        Text hint = CreateText("点击任意位置继续营业", pr, 22, new Color(0.45f, 0.42f, 0.38f), TextAnchor.MiddleCenter, FontStyle.Normal);
        hint.font = stFont;
        RectTransform hr = hint.rectTransform;
        hr.anchorMin = new Vector2(0f, 0f); hr.anchorMax = new Vector2(1f, 0f);
        hr.pivot = new Vector2(0.5f, 0f);
        hr.anchoredPosition = new Vector2(0f, 22f);
        hr.sizeDelta = new Vector2(0f, 36f);
    }

    /// <summary>只关弹窗（供 OnNewDayReset 等系统自动调用）</summary>
    public void CloseSettlement()
    {
        if (settlementRoot != null) Destroy(settlementRoot);
        settlementRoot = null;
    }

    /// <summary>玩家点击结算弹窗 → 关弹窗 → 黑屏过渡 → 跳到第二天</summary>
    private void OnSettlementClicked()
    {
        CloseSettlement();
        StartCoroutine(DayTransitionRoutine());
    }

    /// <summary>黑屏过渡：淡入黑屏 → 显示"第X天" → 淡出 → 继续营业</summary>
    private IEnumerator DayTransitionRoutine()
    {
        // 跳到第二天 9:00（触发 OnNewDay + OnWorkStart，重置所有状态）
        if (GameTimeManager.Instance != null)
            GameTimeManager.Instance.JumpToNextWorkday();

        // 创建全屏黑屏
        GameObject blackScreen = new GameObject("DayTransition", typeof(RectTransform), typeof(Image));
        blackScreen.transform.SetParent(overlayRect, false);
        RectTransform br = blackScreen.GetComponent<RectTransform>();
        br.anchorMin = Vector2.zero; br.anchorMax = Vector2.one;
        br.offsetMin = Vector2.zero; br.offsetMax = Vector2.zero;
        br.SetAsLastSibling();
        Image blackImg = blackScreen.GetComponent<Image>();
        blackImg.color = new Color(0f, 0f, 0f, 0f);
        blackImg.raycastTarget = true; // 挡住所有点击

        // 淡入黑屏
        float fadeTime = 0.4f;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            Color c = blackImg.color;
            c.a = Mathf.Lerp(0f, 1f, t / fadeTime);
            blackImg.color = c;
            yield return null;
        }

        // 显示"第 X 天"
        int day = GameTimeManager.Instance != null ? GameTimeManager.Instance.currentDay : 2;
        Font dayFont = settlementFont != null ? settlementFont : ResolveFont();
        Text dayText = CreateText($"—— 第 {day} 天 ——", br, 72, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
        dayText.font = dayFont;

        // 停留
        float stayTime = dayTransitionDuration - fadeTime * 2f;
        if (stayTime < 0.5f) stayTime = 0.5f;
        yield return new WaitForSeconds(stayTime);

        // 淡出
        t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, t / fadeTime);
            Color c = blackImg.color;
            c.a = alpha;
            blackImg.color = c;
            if (dayText != null)
            {
                Color tc = dayText.color;
                tc.a = alpha;
                dayText.color = tc;
            }
            yield return null;
        }

        Destroy(blackScreen);
        Debug.Log($"[Tutorial] 黑屏过渡完成，进入第 {day} 天");
    }

    // ================================================================
    // 引导结束
    // ================================================================
    private void FinishGuide()
    {
        if (stage == Stage.FreePlay) return; // 幂等：fallback 路径可能重复调用，收尾只做一次
        stage = Stage.FreePlay;
        stageOnNext = null;
        HideHint();
        ClearStageHooks();
        TryBindPersistentHooks();
        // 注意：不归零 completedCount —— 教程那 1 位也是真实接待的（todayRemain 扣过），
        // 归零会导致结算显示 7 位 ≠ 实际 8 位
        approvalProcessing = false; // 兜底：教程各阶段都释放过，但这里再清一次防边界
        StartCoroutine(FinishGuideCleanupRoutine());
        Debug.Log("[Tutorial] 基础引导结束，进入自由审批");
    }

    /// <summary>教程结束收尾：立即关闭所有弹窗和Btn_File2/3 → 教程NPC退场 → 恢复桌面与铃铛</summary>
    private IEnumerator FinishGuideCleanupRoutine()
    {
        NPCBellLogic bell = FindObjectOfType<NPCBellLogic>();

        // 1. 立即关闭教程期间打开的所有弹窗（档案/通关文书等）
        RoomInteract.CloseAllPopups();

        // 2. 立即隐藏 Btn_File2 / Btn_File3 / Btn_4（不等NPC退场动画）
        if (bell != null) bell.HideFileButtons();
        yield return null; // 等一帧让 Destroy/SetActive 生效

        // 3. 教程NPC还在场上：播退场动画（NotifyNpcLeft 会计入今日接待人数）
        if (npcOnStage)
        {
            NpcLeave();
            while (leaveCoroutine != null) yield return null;
        }

        // 4. 恢复桌面初始状态、解锁铃铛，等待玩家手动摇铃开始自由审批
        if (bell != null) bell.OnNpcLeft();
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
