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

    private GameSession _session;
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
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnWorkEnd += HandleWorkEnd;
            GameTimeManager.Instance.OnNewDay += HandleNewDay;
        }
    }

    void Start()
    {
        // Start 时机：所有 prefab 已实例化 → 全局扫 TMP 换成 Legacy Text，再把 Legacy font 全覆盖
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

    /// <summary>下班（18:00）日结：Run session.EndDay(currentDay)，
    /// 自动计算世界值/信任变化、生成次日规则与申请者、state.day++。</summary>
    private void HandleWorkEnd()
    {
        if (_session == null || GameTimeManager.Instance == null) return;
        int day = GameTimeManager.Instance.currentDay;
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
    }

    /// <summary>新一天：NPCBellLogic.ResetForNewDay 会把 _todayNpcIndex 重置为 0，
    /// 对应 EndDay 已 InstallDay 好的 state.npcs 队列，完美衔接。</summary>
    private void HandleNewDay()
    {
        if (_session == null) return;
        var s = _session.GetState();
        Debug.Log($"[GameSessionBridge] OnNewDay → state.day={s.day} 今日申请者={s.todayApplicantCount} " +
            $"今日规则条数={s.npcs?.Count} 纯净={s.purity} 秩序={s.order} 信任={s.trust}");
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
