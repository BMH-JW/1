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

    public int todayRemain;
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

    // 每天早上9点：重置8次
    void OnWorkStart()
    {
        maxDailyCount = 8;
        todayRemain = maxDailyCount;
        Debug.Log($"【OnWorkStart】todayRemain被赋值 = {todayRemain}");
        RefreshNPCState();
        RefreshUI();
    }

    // 每天18点下班
    void OnWorkEnd()
    {
        //npcObj.SetActive(false); //NPC显隐交给铃铛逻辑，注释
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

        //扣完次数更新铃铛按钮状态
        NPCBellLogic bellLogic = FindObjectOfType<NPCBellLogic>();
        if (bellLogic != null)
        {
            bellLogic.CheckBellInteractable();
        }
        return true;
    }

    // 根据剩余次数刷新状态 ——【不再自动控制NPC显示隐藏】
    void RefreshNPCState()
    {
        //bool show = GameTimeManager.Instance.IsWorkTime() && todayRemain > 0;
        //npcObj.SetActive(show);
    }

    void RefreshUI()
    {
        Debug.Log($"【RefreshUI】todayRemain={todayRemain}");
        if (remainText != null)
        {
            remainText.text = $"{todayRemain}";
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