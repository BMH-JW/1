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

        // DailyEvent：按 EndDay 结算后的秩序/纯净区间选事件文本，自动弹弹窗
        TryPopupDailyEvent();
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

        string title = DailyEventConfig.BuildTitle(dayNum, orderAfter, purityAfter);
        string body = dailyEventConfig.BuildDailyEventText(orderAfter, purityAfter);

        // 始终弹窗 —— 有事件显示事件，没事件显示"无新闻报道"
        Debug.Log($"[GameSessionBridge] DailyEvent —— day={dayNum} order={orderAfter:F1} purity={purityAfter:F1}\n标题: {title}\n事件:\n{body}");
        _lastDayEndReport = null;

        // 关掉上一个 DailyEvent 弹窗（防止重复）
        if (_activeDailyEventPopup != null)
        {
            Destroy(_activeDailyEventPopup);
            _activeDailyEventPopup = null;
        }

        GameObject canvasGO = GameObject.Find("Canvas");
        Transform parent = canvasGO != null ? canvasGO.transform : transform.parent;
        _activeDailyEventPopup = Instantiate(dailyEventPrefab, parent);

        PopupPanel panel = _activeDailyEventPopup.GetComponent<PopupPanel>();
        if (panel != null)
        {
            panel.SetSource("DailyEvent");
            panel.SetText(title, body);
        }

        _lastDayEndReport = null;   // 消费一次就清，防止同一天重复触发
    }

    /// <summary>手动触发 DailyEvent 弹窗（绕开事件订阅时序问题，供 TutorialManager 等直接调用）</summary>
    public void TriggerDailyEvent()
    {
        TryPopupDailyEvent();
    }

    /// <summary>游戏是否已结束（供外部查询）。</summary>
    public bool IsGameEnded() => _gameEnded;

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

        // 构造伪 title + body
        int dayNum = _session.GetState().day;
        string title = DailyEventConfig.BuildTitle(dayNum, order, purity);
        string body = dailyEventConfig.BuildDailyEventText(order, purity);

        Debug.Log($"[GameSessionBridge] 🔧 调试弹 DailyEvent —— day={dayNum} order={order:0} purity={purity:0}\n标题: {title}\n事件: {body}");

        GameObject canvasGO = GameObject.Find("Canvas");
        Transform parent = canvasGO != null ? canvasGO.transform : transform.parent;
        _activeDailyEventPopup = Instantiate(dailyEventPrefab, parent);
        PopupPanel panel = _activeDailyEventPopup.GetComponent<PopupPanel>();
        if (panel != null)
        {
            panel.SetSource("DailyEvent_Debug");
            panel.SetText(title, body);
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
