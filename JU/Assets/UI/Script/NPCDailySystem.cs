using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NPCDailyManager : MonoBehaviour
{
    [Header("NPC本体物体")]
    public GameObject npcObj;

    [Header("剩余次数UI文本（普通Text）")]
    public Text remainText;

    [Header("每日最大次数")]
    public int maxDailyCount = 8;

    public static NPCDailyManager Instance;
    private int todayRemain;
    // NPC管理器脚本内
    public event System.Action OnInitComplete;


    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GameTimeManager.Instance.OnWorkStart += OnWorkStart;
        GameTimeManager.Instance.OnWorkEnd += OnWorkEnd;
        GameTimeManager.Instance.OnNewDay += OnNewDay;

        bool workFlag = GameTimeManager.Instance.IsWorkTime();
        Debug.Log($"【调试】是否上班时间:{workFlag}");

        if (workFlag)
        {
            Debug.Log("进入OnWorkStart");
            OnWorkStart();
        }
        else
        {
            Debug.Log("不上班");
        }

        RefreshUI();
        OnInitComplete?.Invoke();
    }

    // 每天早上9点：重置8次 + NPC出现
    void OnWorkStart()
    {
        maxDailyCount = 8;
        todayRemain = maxDailyCount;
        Debug.Log($"【OnWorkStart】todayRemain被赋值 = {todayRemain}");
        RefreshNPCState();
        RefreshUI();
    }

    // 每天18点下班：NPC直接消失
    void OnWorkEnd()
    {
        npcObj.SetActive(false);
    }

    // 新的一天初始化
    void OnNewDay()
    {
        todayRemain = maxDailyCount;
        RefreshNPCState();
        RefreshUI();
    }

    // 玩家互动一次NPC
    public bool InteractNPC()
    {
        // 非上班时间不能互动
        if (!GameTimeManager.Instance.IsWorkTime()) return false;
        if (todayRemain <= 0) return false;

        todayRemain--;
        RefreshUI();
        RefreshNPCState();
        return true;
    }

    // 根据剩余次数决定NPC显隐
    void RefreshNPCState()
    {
        // 上班时间 + 还有次数 → 显示
        bool show = GameTimeManager.Instance.IsWorkTime() && todayRemain > 0;
        npcObj.SetActive(show);
    }

    void RefreshUI()
    {
        Debug.Log($"【RefreshUI】todayRemain={todayRemain}");
        if (remainText != null)
        {
            remainText.text = $"今日剩余NPC：{todayRemain} / 8";
        }
        else
        {
            Debug.LogError("remainText为空，没有拖拽UI文本");
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