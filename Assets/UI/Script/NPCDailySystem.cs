using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JU.GameData;

public class NPCDailyManager : MonoBehaviour
{
    [Header("每天NPC数量随机范围（仅在无 GameSession 时 fallback 用；GameSession 会覆盖）")]
    [Tooltip("每天最多出现的NPC数量下限(含)")]
    [Range(1, 12)] public int minDailyNpc = 3;
    [Tooltip("每天最多出现的NPC数量上限(含)")]
    [Range(1, 12)] public int maxDailyNpc = 6;

    [Header("剩余次数Text组件(拖Text组件)")]
    public Text remainText;

    public static NPCDailyManager Instance;
    public int todayRemain;       // 当前剩余可摇铃次数
    public int dailyNpcCount;     // 今天开局抽的总数(3~6)，供结算判断/NPC数显示用

    private bool _initFinished = false;
    private bool _bellRangToday; // 今天是否已摇过铃：摇铃前人数显示0，首次摇铃后从 dailyNpcCount 开始

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GameTimeManager.Instance.OnWorkStart += OnWorkStart;
        GameTimeManager.Instance.OnWorkEnd += OnWorkEnd;
        GameTimeManager.Instance.OnNewDay += OnNewDay;

        RollDailyNpcCount();
        _bellRangToday = false;
        Debug.Log($"[Start]今日抽定NPC总数={dailyNpcCount}（摇铃前显示0）");
        StartCoroutine(InitDoneLock());
        RefreshUI();
    }

    IEnumerator InitDoneLock()
    {
        yield return new WaitForSeconds(0.05f);
        _initFinished = true;
        Debug.Log($"初始化完成，铃铛打开。");
    }

    void OnWorkStart()
    {
        if (!_initFinished) return;
        RollDailyNpcCount();
        _bellRangToday = false;
        RefreshUI();
        Debug.Log($"[OnWorkStart]今日抽定NPC总数={dailyNpcCount}（摇铃前显示0）");
    }

    void OnWorkEnd()
    {
    }

    void OnNewDay()
    {
        if (!_initFinished) return;
        RollDailyNpcCount();
        _bellRangToday = false;
        RefreshUI();
        Debug.Log($"[OnNewDay]今日抽定NPC总数={dailyNpcCount}（摇铃前显示0）");
    }

    /// <summary>优先读 GameSession（配置里的 minDailyApplicants/maxDailyApplicants + todayApplicantCount）；
    /// 没有 GameSessionBridge 就 fallback 旧的 minDailyNpc/maxDailyNpc 随机抽。</summary>
    private void RollDailyNpcCount()
    {
        var bridge = GameSessionBridge.Instance;
        if (bridge != null && bridge.Current != null)
        {
            // 接入 GameData 配置：用 session 已经生成好的 todayApplicantCount
            dailyNpcCount = bridge.Current.GetState().todayApplicantCount;
            todayRemain = dailyNpcCount;
            Debug.Log($"[NPCDaily] 使用 GameSession 数据 todayApplicantCount={dailyNpcCount}");
            return;
        }

        // fallback：旧硬编码逻辑
        int lo = Mathf.Max(1, minDailyNpc);
        int hi = Mathf.Max(lo, maxDailyNpc);
        dailyNpcCount = Random.Range(lo, hi + 1); // 含上限
        todayRemain = dailyNpcCount;
    }

    public bool InteractNPC()
    {
        Debug.Log($"[InteractNPC]当前todayRemain={todayRemain}");
        if (todayRemain <= 0)
        {
            return false;
        }
        // 首次摇铃：人数从今天抽定的 dailyNpcCount 开始显示
        _bellRangToday = true;
        // 先显示当前剩余次数（第一次点击显示 dailyNpcCount），再扣减
        RefreshUI();
        todayRemain -= 1;
        // 扣减后如果归0，刷新显示为0
        if (todayRemain <= 0)
        {
            RefreshUI();
        }

        NPCBellLogic bellLogic = FindObjectOfType<NPCBellLogic>();
        if (bellLogic != null)
        {
            bellLogic.CheckBellInteractable();
        }
        return true;
    }

    void RefreshUI()
    {
        if (remainText != null)
        {
            // 今天还没摇过铃时显示0，首次摇铃后显示真实剩余人数
            remainText.text = _bellRangToday ? todayRemain.ToString() : "0";
        }
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance == null) return;
        GameTimeManager.Instance.OnWorkStart -= OnWorkStart;
        GameTimeManager.Instance.OnWorkEnd -= OnWorkEnd;
        GameTimeManager.Instance.OnNewDay -= OnNewDay;
    }
}
