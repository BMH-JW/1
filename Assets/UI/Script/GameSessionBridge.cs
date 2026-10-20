using UnityEngine;
using System.Collections.Generic;
using JU.GameData;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 把 Assets/GameData/Config/DefaultGameDataConfig.asset 的配置加载成运行时 GameSession 局。
/// 挂在 RoomScene 的 Canvas 或任意根物体上（整个游戏一个）。
/// 其他脚本用 GameSessionBridge.Current 就能拿到 GameSession 实例来：
///   - 取当前每日申请者 (GetState().npcs)
///   - 取每日规则文本 (GetRuleAnnouncement)
///   - 取某个 NPC 的档案/文书 (GetProfile / GetDocument)
///   - 提交审批 (SubmitApplication)
///   - 日结 (EndDay)
///
/// 另：本脚本提供 ConvertTmpToLegacy() 静态方法 —— 运行时把任何 TMP_Text 组件
/// 换成 Unity 内置 Legacy Text（Font.CreateDynamicFontFromOSFont 路线永远比 TMP 稳）。
/// </summary>
public class GameSessionBridge : MonoBehaviour
{
    [Header("数据配置")]
    [Tooltip("GameDataConfig 资产；留空会自动 Resources/FindObjectsOfType 兜底查找 DefaultGameDataConfig.asset")]
    public GameDataConfig configAsset;

    [Header("运行参数")]
    [Tooltip("随机种子；-1 表示每次开局用 DateTime.UtcNow 生成，保证每局不同")]
    public int seed = -1;

    [Header("每日事件")]
    [Tooltip("DailyEventConfig 资产 —— 填好各区间事件文本；Create → GameData → Daily Event Config")]
    public DailyEventConfig dailyEventConfig;
    [Tooltip("DailyEvent 专用弹窗 prefab（RoomScene 里 PopupPanel7 那个）；留空则不自动弹")]
    public GameObject dailyEventPrefab;

    private GameSession _session;
    private DayEndReport _lastDayEndReport;   // HandleWorkEnd 里存，HandleNewDay 里用来触发 DailyEvent
    private bool _gameEnded = false;           // 游戏结束标记 —— 结束后跳过 DailyEvent

    // 最近一次日结的新闻数据缓存 —— 第 2 天起新闻不再自动弹，
    // 改由玩家点「审查规则」(Btn_7) 打开弹窗时用 FillLatestDailyNews 填充。
    // 事件文案在 DoEndDay 时一次性抽定并缓存，保证同一天弹几次都是同一份内容。
    private bool _pendingNewsValid;
    private int _pendingNewsDay;        // 显示用「第 N 天」
    private int _pendingSettledDay;     // 结算的是第几天 —— 判定是否只显示标题「新闻」
    private float _pendingNewsOrder;
    private float _pendingNewsPurity;
    private string _pendingOrderEvent;
    private string _pendingPurityEvent;
    public static GameSessionBridge Instance { get; private set; }
    public GameSession Current => _session;

    // 全局中文字体缓存（RoomScene 启动时创建一次，全项目复用）
    public static Font GlobalCjkLegacyFont { get; private set; }

    // ---------------------------------------------------------------------------
    // 运行时 TMP → Legacy 转换（彻底绕开 TMP 字体 API 的各种坑）
    // ---------------------------------------------------------------------------

    /// <summary>把一个 TMP_Text 组件运行时换成 Legacy Text。
    /// 保存原属性（text/alignment/fontSize/color/raycast），Disable TMP，AddComponent Text，Destroy TMP。
    /// 返回新加的 Legacy Text；原来的 TMP_Text 组件被销毁。</summary>
    public static Text ConvertTmpToLegacy(TMP_Text tmp)
    {
        if (tmp == null) return null;
        if (tmp is TextMeshPro) return null;
        if (!tmp.gameObject.scene.IsValid()) return null;

        // 【关键】DestroyImmediate 之后 tmp 就是 destroyed object，
        // 访问 tmp.gameObject / tmp.任何成员 都会 MissingReferenceException
        // → 所有要用到的值、包括 GameObject 引用，必须在 DestroyImmediate 之前全部保存
        GameObject go = tmp.gameObject;
        string txt = tmp.text;
        float fs = tmp.fontSize;
        Color col = tmp.color;
        bool raycast = tmp.raycastTarget;
        var align = tmp.alignment;
        bool autoSize = false;
        float fsMax = fs;
        try { autoSize = tmp.enableAutoSizing; fsMax = tmp.fontSizeMax; } catch { }

        DestroyImmediate(tmp);

        var legacy = go.AddComponent<Text>();
        if (legacy == null) return null;

        legacy.text = txt;
        legacy.alignment = TmpAlignToUnityAlign(align);
        legacy.fontSize = autoSize ? (int)fsMax : (int)fs;
        legacy.color = col;
        legacy.raycastTarget = raycast;
        legacy.horizontalOverflow = HorizontalWrapMode.Wrap;
        legacy.verticalOverflow = VerticalWrapMode.Overflow;

        Font font = GlobalCjkLegacyFont;
        if (font == null) font = EnsureLegacyFont();
        if (font != null)
        {
            legacy.font = font;
            if (GlobalCjkLegacyFont == null) GlobalCjkLegacyFont = font;
        }
        else
        {
            Debug.LogWarning($"[ConvertTmpToLegacy] 找不到可用系统字体！{go.name} 的 Legacy Text 字体为空");
        }

        return legacy;
    }

    /// <summary>独立兜底：Font.CreateDynamicFontFromOSFont —— 任何 Windows 机器必通。
    /// Lazy static 缓存：同一个 Font 实例被所有转换共用，不会每次重新创建。</summary>
    static Font _cachedLegacyFont;
    static Font EnsureLegacyFont()
    {
        if (_cachedLegacyFont != null) return _cachedLegacyFont;

        string[] candidates = { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "SimSun", "宋体", "Arial Unicode MS", "Arial" };
        foreach (var name in candidates)
        {
            if (string.IsNullOrEmpty(name)) continue;
            Font f = Font.CreateDynamicFontFromOSFont(name, 48);
            if (f != null) { _cachedLegacyFont = f; return f; }
        }
        return null;
    }

    /// <summary>TMP TextAlignmentOptions → Unity TextAnchor。
    /// TMP 3.0.7 的 TextAlignmentOptions 是 Flag 枚举，值=Top(16)/Middle(32)/Bottom(64) | Left(1)/Center(2)/Right(4) 按位组合。
    /// 我们按位提取行（Top/Middle/Bottom）和列（Left/Center/Right）再组合成 TextAnchor。</summary>
    static TextAnchor TmpAlignToUnityAlign(TextAlignmentOptions a)
    {
        int v = (int)a;
        bool isTop    = (v & 16) != 0;
        bool isMiddle = (v & 32) != 0;
        bool isBottom = (v & 64) != 0;
        bool isLeft   = (v & 1) != 0;
        bool isCenter = (v & 2) != 0;
        bool isRight  = (v & 4) != 0;

        if (isTop)
        {
            if (isLeft)   return TextAnchor.UpperLeft;
            if (isCenter) return TextAnchor.UpperCenter;
            if (isRight)  return TextAnchor.UpperRight;
        }
        if (isMiddle)
        {
            if (isLeft)   return TextAnchor.MiddleLeft;
            if (isCenter) return TextAnchor.MiddleCenter;
            if (isRight)  return TextAnchor.MiddleRight;
        }
        if (isBottom)
        {
            if (isLeft)   return TextAnchor.LowerLeft;
            if (isCenter) return TextAnchor.LowerCenter;
            if (isRight)  return TextAnchor.LowerRight;
        }
        return TextAnchor.MiddleCenter;
    }

    /// <summary>全场景扫，把所有 TMP_Text 换成 Legacy Text。
    /// 先 ToList 收集（不能在遍历 Destroy 的同时 foreach 同一个数组）。</summary>
    public static int ConvertAllTmpToLegacyInScene()
    {
        // ToList 很关键 —— FindObjectsOfType 返回的数组如果遍历时 DestroyImmediate 其中一个，
        // 下一轮访问那个索引就是 destroyed object → MissingReferenceException
        var list = new List<TMP_Text>(FindObjectsOfType<TMP_Text>(true));
        int count = 0;
        foreach (var t in list)
        {
            if (t == null) continue;  // 已经被这一轮转换里 DestroyImmediate 了
            if (ConvertTmpToLegacy(t) != null) count++;
        }
        Debug.Log($"[GameSessionBridge] TMP_Text → Legacy Text：{count} 个");
        return count;
    }

    // ---------------------------------------------------------------------------
    // 生命周期
    // ---------------------------------------------------------------------------

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (configAsset == null)
        {
            configAsset = FindObjectOfType<GameDataConfig>();
#if UNITY_EDITOR
            if (configAsset == null)
            {
                configAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataConfig>(
                    "Assets/GameData/Config/DefaultGameDataConfig.asset");
            }
#endif
            if (configAsset == null)
            {
                Debug.LogError("[GameSessionBridge] 找不到 GameDataConfig 资产，GameSession 无法初始化。" +
                    "请在 RoomScene 的 GameSessionBridge 组件里手动拖 configAsset 槽位，" +
                    "或确认 Assets/GameData/Config/DefaultGameDataConfig.asset 存在。");
                return;
            }
        }

        int actualSeed = seed < 0 ? System.DateTime.UtcNow.GetHashCode() : seed;
        _session = GameSession.StartNewGame(configAsset.data, actualSeed);
        Debug.Log($"[GameSessionBridge] GameSession 启动 seed={actualSeed} day={_session.GetState().day} " +
            $"今日申请者={_session.GetState().todayApplicantCount} 规则={_session.GetRuleAnnouncement()}");

        // 预创建全局 Legacy 中文字体（Font.CreateDynamicFontFromOSFont —— Unity 内置，任何 Windows 机器必通）
        BuildGlobalCjkLegacyFont();

        // 订阅游戏时钟事件
        TrySubscribeTimeEvents();
    }

    private void TrySubscribeTimeEvents()
    {
        if (GameTimeManager.Instance == null)
        {
            Debug.LogWarning("[GameSessionBridge] TrySubscribeTimeEvents —— GameTimeManager.Instance 还没初始化，跳过订阅，Start 里再补");
            return;
        }
        GameTimeManager.Instance.OnWorkEnd -= HandleWorkEnd;   // 防重复
        GameTimeManager.Instance.OnNewDay -= HandleNewDay;
        GameTimeManager.Instance.OnWorkEnd += HandleWorkEnd;
        GameTimeManager.Instance.OnNewDay += HandleNewDay;
        Debug.Log("[GameSessionBridge] ✅ 已订阅 GameTimeManager.OnWorkEnd / OnNewDay");
    }

    void Start()
    {
        // Start 时机：所有 GameObject 的 Awake 都跑完了 → GameTimeManager.Instance 一定有了
        TrySubscribeTimeEvents();

        // 全局扫 TMP 换成 Legacy Text，再把 Legacy font 全覆盖
        ConvertAllTmpToLegacyInScene();
        ApplyGlobalLegacyFont();
    }

    // ---------------------------------------------------------------------------
    // 字体：只走 Legacy Font 这一条（永远通），彻底放弃 TMP FontAsset
    // ---------------------------------------------------------------------------

    /// <summary>Font.CreateDynamicFontFromOSFont —— Unity 内置 API，任何 Windows 机器必通。
    /// 候选顺序：微软雅黑 → 黑体 → 宋体 → Arial Unicode MS → Arial</summary>
    private void BuildGlobalCjkLegacyFont()
    {
        string[] candidates = { "Microsoft YaHei", "微软雅黑", "SimHei", "黑体", "SimSun", "宋体", "Arial Unicode MS", "Arial" };
        foreach (var name in candidates)
        {
            if (string.IsNullOrEmpty(name)) continue;
            Font f = Font.CreateDynamicFontFromOSFont(name, 48);
            if (f == null) continue;
            GlobalCjkLegacyFont = f;
            Debug.Log($"[GameSessionBridge] Legacy 中文字体就绪：「{f.name}」 —— 全场景可用");
            return;
        }
        Debug.LogError("[GameSessionBridge] 系统找不到任何可用字体！中文全是方块。");
    }

    /// <summary>全场景所有 Legacy Text → 统一换成 GlobalCjkLegacyFont</summary>
    private void ApplyGlobalLegacyFont()
    {
        if (GlobalCjkLegacyFont == null) return;
        int count = 0;
        foreach (var t in FindObjectsOfType<Text>(true))
            if (t.font != GlobalCjkLegacyFont) { t.font = GlobalCjkLegacyFont; count++; }
        Debug.Log($"[GameSessionBridge] 全局 Legacy 中文字体覆盖：{count} 个 Text");
    }

    // ---------------------------------------------------------------------------
    // 日结 / 新一天
    // ---------------------------------------------------------------------------

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnWorkEnd -= HandleWorkEnd;
            GameTimeManager.Instance.OnNewDay -= HandleNewDay;
        }
    }

    /// <summary>下班（18:00）自动日结 —— 走统一的 DoEndDay。</summary>
    private void HandleWorkEnd()
    {
        if (_session == null || GameTimeManager.Instance == null) return;
        int day = GameTimeManager.Instance.currentDay;
        DoEndDay(day);
    }

    /// <summary>游戏结束：弹窗 + 冻结游戏 + 禁用交互。</summary>
    private void TriggerGameOver(DayEndReport report)
    {
        // 优先用 DailyEvent prefab（如果没配单独的 gameOverPrefab），否则找 PopupPanel
        GameObject prefab = dailyEventPrefab;
        if (prefab == null)
        {
            var pan = FindObjectOfType<PopupPanel>();
            if (pan != null) prefab = pan.gameObject;
        }
        if (prefab == null)
        {
            Debug.LogError("[GameSessionBridge] TriggerGameOver —— 没有任何 PopupPanel prefab 可用，放弃弹窗");
            return;
        }

        // 关掉 DailyEvent 弹窗（如果有的话）
        if (_activeDailyEventPopup != null)
        {
            Destroy(_activeDailyEventPopup);
            _activeDailyEventPopup = null;
        }

        GameObject canvas = GetComponentInParent<Canvas>()?.gameObject;
        if (canvas == null) canvas = GameObject.Find("Canvas");
        if (canvas == null) { Debug.LogError("[GameSessionBridge] 找不到 Canvas，GameOver 弹窗无父节点"); return; }

        GameObject popupGO = Instantiate(prefab, canvas.transform, false);
        popupGO.name = "Popup_GameOver";

        PopupPanel panel = popupGO.GetComponent<PopupPanel>();
        if (panel == null) panel = popupGO.AddComponent<PopupPanel>();
        panel.SetSource("GameOver");

        string title = report.isWin ? "🎉 乌托邦达成！" : "💀 世界崩塌";
        string body = $"第 {report.settledDay} 日\n\n{report.gameEndReason}\n\n" +
                      $"日结后秩序={report.after.order:0}  纯净={report.after.purity:0}  信任={report.after.trust}";
        panel.SetText(title, body);

        // 冻结游戏：禁用 GameTimeManager 计时器 + 禁用 RoomInteract 等
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.isPaused = true;
        }
        // 禁用所有 RoomInteract
        foreach (var ri in FindObjectsOfType<RoomInteract>()) ri.enabled = false;
        foreach (var bell in FindObjectsOfType<NPCBellLogic>()) bell.enabled = false;
        Debug.Log("[GameSessionBridge] ✅ 游戏已冻结（计时器+交互全部禁用），点击弹窗关闭按钮可手动交互");
    }

    /// <summary>新一天：NPCBellLogic.ResetForNewDay 会把 _todayNpcIndex 重置为 0，
    /// 对应 EndDay 已 InstallDay 好的 state.npcs 队列，完美衔接。</summary>
    private void HandleNewDay()
    {
        if (_session == null) return;
        var s = _session.GetState();
        Debug.Log($"[GameSessionBridge] OnNewDay → state.day={s.day} 今日申请者={s.todayApplicantCount} " +
            $"今日规则条数={s.npcs?.Count} 纯净={s.purity} 秩序={s.order} 信任={s.trust}");

        // 只有第一天的新闻自动弹（且只显示标题「新闻」）；第 2 天起不再自动弹 ——
        // 改成玩家点「审查规则」(Btn_7) 按钮打开弹窗时，再用 FillLatestDailyNews 填充显示
        if (_lastDayEndReport != null && _lastDayEndReport.settledDay <= 1)
            TryPopupDailyEvent();
        else
            Debug.Log("[GameSessionBridge] 第 2 天起新闻不再自动弹出 —— 等玩家点按钮时用 FillLatestDailyNews 填充");
    }

    private GameObject _activeDailyEventPopup;

    public void EnsureSessionReady()
    {
        if (_session != null) return;

        Debug.LogWarning("[GameSessionBridge] EnsureSessionReady —— _session 为 null，兜底初始化");
        if (configAsset == null)
        {
            configAsset = FindObjectOfType<GameDataConfig>();
#if UNITY_EDITOR
            if (configAsset == null)
                configAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameDataConfig>(
                    "Assets/GameData/Config/DefaultGameDataConfig.asset");
#endif
        }
        if (configAsset == null)
        {
            Debug.LogError("[GameSessionBridge] 兜底也找不到 configAsset，放弃");
            return;
        }

        int actualSeed = seed < 0 ? System.DateTime.UtcNow.GetHashCode() : seed;
        _session = GameSession.StartNewGame(configAsset.data, actualSeed);
        Debug.Log($"[GameSessionBridge] 兜底 GameSession 启动 seed={actualSeed} day={_session.GetState().day} " +
            $"今日申请者={_session.GetState().todayApplicantCount}");

        TrySubscribeTimeEvents();
    }

    private void TryPopupDailyEvent()
    {
        if (_gameEnded)
        {
            Debug.Log("[GameSessionBridge] TryPopupDailyEvent —— 游戏已结束，跳过 DailyEvent");
            return;
        }
        Debug.Log("[GameSessionBridge] TryPopupDailyEvent 被调用");
        EnsureSessionReady();   // 兜底 session 初始化
        if (_session == null)
        {
            Debug.LogError("[GameSessionBridge] TryPopupDailyEvent —— session 还是 null，放弃");
            return;
        }
        if (dailyEventConfig == null)
        {
            Debug.LogWarning("[GameSessionBridge] TryPopupDailyEvent —— dailyEventConfig 没配，跳过");
            return;
        }
        if (dailyEventPrefab == null)
        {
            Debug.LogWarning("[GameSessionBridge] TryPopupDailyEvent —— dailyEventPrefab 没拖，跳过");
            return;
        }

        // EndDay 只在 HandleWorkEnd（18:00 自动）或 TutorialManager（手动结算）里显式调。
        // TryPopupDailyEvent 不再隐式兜底 EndDay —— 避免 JumpToNextWorkday.currentDay++ 
        // 和 EndDay.state.day++ 两个计数器各跳各的导致错位。
        if (_lastDayEndReport == null)
        {
            Debug.LogError("[GameSessionBridge] TryPopupDailyEvent —— _lastDayEndReport 为空，说明 EndDay 从未被调用！" +
                "请确保 HandleWorkEnd 或 TutorialManager 在调 DailyEvent 之前先调 EndDay。");
            return;
        }
        if (_lastDayEndReport == null || !_lastDayEndReport.success)
        {
            Debug.LogWarning($"[GameSessionBridge] TryPopupDailyEvent —— EndDay 没成功 " +
                $"report={(_lastDayEndReport != null ? _lastDayEndReport.error : "null")}，跳过");
            return;
        }

        // DoEndDay 已统一处理游戏结束（TriggerGameOver + _gameEnded=true），
        // TryPopupDailyEvent 开头也有 _gameEnded 守卫，不会走到这里。

        var report = _lastDayEndReport;
        float orderAfter = report.after.order;
        float purityAfter = report.after.purity;
        int dayNum = report.after.day;

        // 事件文本优先用 DoEndDay 缓存的 —— 保证同一天弹几次都是同一份内容
        string orderEvent = _pendingNewsValid ? _pendingOrderEvent : dailyEventConfig.ResolveOrderEvent(orderAfter);
        string purityEvent = _pendingNewsValid ? _pendingPurityEvent : dailyEventConfig.ResolvePurityEvent(purityAfter);
        string eventText = DailyEventConfig.JoinEventText(orderEvent, purityEvent);

        // 始终弹窗；只有真的抽到事件时才显示事件正文
        Debug.Log($"[GameSessionBridge] DailyEvent —— day={dayNum} 秩序={orderAfter:F1} 纯净={purityAfter:F1}\n事件: " +
            (string.IsNullOrEmpty(eventText) ? "（本日无事件）" : eventText));
        _lastDayEndReport = null;

        // 关掉上一个 DailyEvent 弹窗（防止重复）
        if (_activeDailyEventPopup != null)
        {
            Destroy(_activeDailyEventPopup);
            _activeDailyEventPopup = null;
        }

        Transform parent = ResolvePopupParent();
        _activeDailyEventPopup = Instantiate(dailyEventPrefab, parent);
        _activeDailyEventPopup.transform.SetAsLastSibling();   // 确保盖在房间之上
        _activeDailyEventPopup.SetActive(true);

        PopupPanel panel = _activeDailyEventPopup.GetComponent<PopupPanel>();
        if (panel != null)
        {
            panel.SetSource("DailyEvent");
            ApplyDailyEventText(panel, dayNum, orderAfter, purityAfter, orderEvent, purityEvent, report.settledDay <= 1);
        }

        var rt = (RectTransform)_activeDailyEventPopup.transform;
        Debug.Log($"[DailyEvent] 弹窗已生成 —— parent={parent.name} sibling={rt.GetSiblingIndex()}/{parent.childCount - 1} " +
            $"size={rt.rect.size} active={_activeDailyEventPopup.activeInHierarchy}");

        _lastDayEndReport = null;   // 消费一次就清，防止同一天重复触发
    }

    /// <summary>手动触发 DailyEvent 弹窗（绕开事件订阅时序问题，供 TutorialManager 等直接调用）</summary>
    public void TriggerDailyEvent()
    {
        TryPopupDailyEvent();
    }

    /// <summary>把最近一次日结的新闻填充进指定弹窗 —— 玩家点「审查规则」(Btn_7) 打开弹窗时用。
    /// 第 1 天（settledDay&lt;=1）或还没有日结数据时，只显示标题「新闻」，其余框全部隐藏。</summary>
    public bool FillLatestDailyNews(PopupPanel panel)
    {
        if (panel == null) return false;

        panel.SetSource("DailyEvent");

        if (!_pendingNewsValid)
        {
            // 还没有日结数据（第 1 天 / 新手教程）—— 按"只显示标题「新闻」"处理，其他文字框全部不展示
            ApplyDailyEventText(panel, 0, 0f, 0f, null, null, true);
            Debug.Log("[GameSessionBridge] FillLatestDailyNews —— 还没有日结新闻数据 → 只显示标题「新闻」");
            return true;
        }

        ApplyDailyEventText(panel, _pendingNewsDay, _pendingNewsOrder, _pendingNewsPurity,
            _pendingOrderEvent, _pendingPurityEvent, _pendingSettledDay <= 1);
        Debug.Log($"[GameSessionBridge] FillLatestDailyNews —— 填充第 {_pendingNewsDay} 天新闻" +
            $"（settledDay={_pendingSettledDay} → {(_pendingSettledDay <= 1 ? "只显示标题「新闻」" : "显示完整内容")}）");
        return true;
    }

    /// <summary>关掉新闻弹窗（玩家点 Btn_6 打开自己的弹窗时调用）。</summary>
    public void CloseDailyEventPopup()
    {
        if (_activeDailyEventPopup == null) return;
        Destroy(_activeDailyEventPopup);
        _activeDailyEventPopup = null;
        Debug.Log("[GameSessionBridge] CloseDailyEventPopup —— 已关闭新闻弹窗");
    }

    /// <summary>游戏是否已结束（供外部查询）。</summary>
    public bool IsGameEnded() => _gameEnded;

    /// <summary>定位弹窗的父节点。
    /// 关键：RoomScene 的 Canvas 名字是 "Canvas_Room"（不是 "Canvas"），
    /// 而 GameSessionBridge 就挂在 Canvas_Room 这个根对象上。
    /// 若用 GameObject.Find("Canvas") 会返回 null，兜底 transform.parent 也是 null（根对象无父），
    /// 结果 Instantiate(prefab, null) 把弹窗丢到场景根节点 → 不在任何 Canvas 下 → 永远不显示。
    /// 正确做法：直接取自己所在（或最近上级）的 Canvas 作为父节点。</summary>
    private Transform ResolvePopupParent()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        return canvas != null ? canvas.transform : transform;
    }

    /// <summary>把 DailyEvent 的文字写进弹窗 —— 代码只管"往哪个框写什么"，绝不动框的位置/尺寸。
    /// 内容：标题固定「新闻」；day = 第 N 天；order / purity 都带上"秩序/纯净"前缀；
    /// 事件正文只在真的抽到事件时才显示（没事件就是空，不留占位文字）。
    /// onlyNewsTitle = true（第 1 天 / 新手教程期间）时只保留标题「新闻」，其余框全部留空隐藏。
    /// 预制体里配了「文字框 ↔ 内容键」（PopupPanel.documentFields）就按 key 分派；
    /// 没配则退回 TitleText / ContentText 两个默认框。</summary>
    private static void ApplyDailyEventText(PopupPanel panel, int day, float order, float purity,
        string orderEvent, string purityEvent, bool onlyNewsTitle)
    {
        string eventText = DailyEventConfig.JoinEventText(orderEvent, purityEvent);

        var values = new Dictionary<string, string>
        {
            { "title",       "新闻" },                               // 弹窗标题
            { "day",         $"第 {day} 天" },                       // 第几天
            { "order",       $"秩序 {Mathf.RoundToInt(order)}" },    // 数值带上名字，避免看不出是什么值
            { "purity",      $"纯净 {Mathf.RoundToInt(purity)}" },
            { "content",     eventText },                            // 事件正文（没抽到事件则为空）
            { "event",       eventText },
            { "body",        eventText },
            { "orderEvent",  orderEvent ?? "" },                     // 只要秩序那条事件
            { "purityEvent", purityEvent ?? "" },                    // 只要纯净那条事件
        };

        // 第 1 天（或新手教程期间）：只保留标题「新闻」，日期/数值/事件全部不显示
        bool inTutorial = TutorialManager.Instance != null && !TutorialManager.Instance.IsFreePlay;
        bool onlyNews = onlyNewsTitle || inTutorial;
        if (onlyNews)
            foreach (var k in new List<string>(values.Keys))
                if (k != "title") values[k] = "";

        // 默认两个框（没配文字框时就用它们）
        var boxes = panel.documentFields;
        if (boxes == null || boxes.Count == 0)
        {
            panel.SetText(values["title"], values["content"]);
            return;
        }

        // 配了文字框 → 内容全部走键位；默认两个框留空，免得和文字框叠字
        panel.SetText("", "");

        panel.SetDocumentFields(values);
        HideEmptyFieldBoxes(boxes, values);

        Debug.Log($"[DailyEvent] 按文字框写入内容键 —— {values.Count} 个键" +
            (onlyNewsTitle ? "（第 1 天：只显示标题「新闻」）" : inTutorial ? "（新手教程中：只显示标题「新闻」）" : "") +
            $"\n可用键：title / day / order / purity / content(event,body) / orderEvent / purityEvent");
    }

    /// <summary>把值为空的文字框隐藏掉 —— 免得露出预制体里的默认文字
    ///（例如：本日无事件时的事件框、新手教程里的日期/数值框）</summary>
    private static void HideEmptyFieldBoxes(List<PopupTextField> boxes, Dictionary<string, string> values)
    {
        foreach (var f in boxes)
        {
            if (f == null || f.slotGO == null || string.IsNullOrEmpty(f.fieldName)) continue;
            bool visible = values.TryGetValue(f.fieldName, out string v) && !string.IsNullOrEmpty(v);
            if (f.slotGO.activeSelf != visible) f.slotGO.SetActive(visible);
        }
    }

    // ================================================================
    // 🔧 调试入口（DailyEventDebugger 调用）
    // ================================================================

    /// <summary>直接覆盖 GameSession 的秩序/纯净值（绕过正规 EndDay 流程）。</summary>
    public void ForceSetWorldValues(float order, float purity)
    {
        EnsureSessionReady();
        if (_session == null) { Debug.LogError("[GameSessionBridge] ForceSetWorldValues —— session null"); return; }
        _session.SetWorldValues(order, purity);
        Debug.Log($"[GameSessionBridge] 🔧 调试：强制 order={order:0} purity={purity:0}");
    }

    /// <summary>用指定的 order/purity 值直接弹 DailyEvent（不走 EndDay）。</summary>
    public void ForcePopupDailyEventWithValues(float order, float purity)
    {
        EnsureSessionReady();
        if (_session == null || dailyEventConfig == null || dailyEventPrefab == null)
        {
            Debug.LogError("[GameSessionBridge] ForcePopupDailyEventWithValues —— session/config/prefab 缺一个");
            return;
        }
        if (_gameEnded) { Debug.Log("[GameSessionBridge] 游戏已结束，跳过"); return; }

        // 关掉之前的 DailyEvent
        if (_activeDailyEventPopup != null) { Destroy(_activeDailyEventPopup); _activeDailyEventPopup = null; }

        // 构造 day/数值 + 事件文本
        int dayNum = _session.GetState().day;
        string orderEvent = dailyEventConfig.ResolveOrderEvent(order);
        string purityEvent = dailyEventConfig.ResolvePurityEvent(purity);
        string eventText = DailyEventConfig.JoinEventText(orderEvent, purityEvent);

        Debug.Log($"[GameSessionBridge] 🔧 调试弹 DailyEvent —— day={dayNum} 秩序={order:0} 纯净={purity:0}\n事件: " +
            (string.IsNullOrEmpty(eventText) ? "（无事件）" : eventText));

        Transform parent = ResolvePopupParent();
        _activeDailyEventPopup = Instantiate(dailyEventPrefab, parent);
        PopupPanel panel = _activeDailyEventPopup.GetComponent<PopupPanel>();
        if (panel != null)
        {
            panel.SetSource("DailyEvent_Debug");
            ApplyDailyEventText(panel, dayNum, order, purity, orderEvent, purityEvent, false);
        }
    }

    /// <summary>手动触发 EndDay 结算 —— TutorialManager 在 JumpToNextWorkday 之前调这个。</summary>
    /// 内部逻辑和 HandleWorkEnd（18:00 自动结算）一模一样。</summary>
    public void ManualEndDay()
    {
        EnsureSessionReady();
        if (_session == null)
        {
            Debug.LogError("[GameSessionBridge] ManualEndDay —— _session 为 null，放弃");
            return;
        }
        var s = _session.GetState();
        int day = s.day;   // 用 session 的 state.day，不用 GameTimeManager.currentDay（还没 Jump）
        Debug.Log($"[GameSessionBridge] ManualEndDay —— 手动结算 day={day}");
        DoEndDay(day);
    }

    /// <summary>EndDay 的统一执行入口 —— 18:00 自动结算和手动结算都走这。</summary>
    private void DoEndDay(int day)
    {
        var report = _session.EndDay(day);
        if (!report.success)
        {
            Debug.LogWarning($"[GameSessionBridge] EndDay 失败 day={day} → {report.error}");
            return;
        }
        Debug.Log($"[GameSessionBridge] === 日结 Day {report.settledDay} 成功 ===");
        Debug.Log($"  合规 {report.compliantCount}  /  违规 {report.violationCount}  /  未处理离开 {report.unprocessedCount}");
        Debug.Log($"  纯净 {report.actualPurityChange:+0.###;-0.###;0}  /  秩序 {report.actualOrderChange:+0.###;-0.###;0}  /  信任 {report.trustChange:+0}");
        Debug.Log($"  日结后 day={report.after.day}  今日申请者={report.after.todayApplicantCount}");
        Debug.Log($"  日结日志:\n{string.Join("\n", report.log)}");
        _lastDayEndReport = report;

        // 缓存本次日结的新闻数据 —— 第 1 天自动弹（只显示标题「新闻」），
        // 第 2 天起不自动弹，等玩家点 Btn_7 时用 FillLatestDailyNews 填充
        _pendingNewsValid  = true;
        _pendingSettledDay = report.settledDay;
        _pendingNewsDay    = report.after.day;
        _pendingNewsOrder  = report.after.order;
        _pendingNewsPurity = report.after.purity;
        _pendingOrderEvent  = dailyEventConfig != null ? dailyEventConfig.ResolveOrderEvent(_pendingNewsOrder)  : "";
        _pendingPurityEvent = dailyEventConfig != null ? dailyEventConfig.ResolvePurityEvent(_pendingNewsPurity) : "";

        // 游戏结束判定
        if (report.gameEnded)
        {
            Debug.LogWarning($"[GameSessionBridge] 🎮 游戏结束！isWin={report.isWin} 原因={report.gameEndReason}");
            _gameEnded = true;
            TriggerGameOver(report);
        }
    }

    /// <summary>
    /// 日结后自动重置 GameSession 的 day/dayApplicantCount 状态（调用 EndDay 之后用）。
    /// RoomScene 里 GameTimeManager.OnNewDay 时调这个。
    /// </summary>
    public void AfterEndDay()
    {
        if (_session == null) return;
        var s = _session.GetState();
        Debug.Log($"[GameSessionBridge] 日结后 state: day={s.day} todayApplicantCount={s.todayApplicantCount}");
    }
}
