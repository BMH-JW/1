using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NPCDailyManager : MonoBehaviour
{
    private const int MAX_DAILY = 8;
    [Header("剩余次数Text组件(拖Text组件)")]
    public Text remainText;

    public static NPCDailyManager Instance;
    public int todayRemain;

    private bool _initFinished = false;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        GameTimeManager.Instance.OnWorkStart += OnWorkStart;
        GameTimeManager.Instance.OnWorkEnd += OnWorkEnd;
        GameTimeManager.Instance.OnNewDay += OnNewDay;

        todayRemain = MAX_DAILY;
        Debug.Log($"[Start]初始化 todayRemain = {todayRemain}");
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
        todayRemain = MAX_DAILY;
        RefreshUI();
        Debug.Log($"[OnWorkStart] todayRemain={todayRemain}");
    }

    void OnWorkEnd()
    {
    }

    void OnNewDay()
    {
        if (!_initFinished) return;
        todayRemain = MAX_DAILY;
        RefreshUI();
        Debug.Log($"[OnNewDay] todayRemain={todayRemain}");
    }

    public bool InteractNPC()
    {
        Debug.Log($"[InteractNPC]当前todayRemain={todayRemain}");
        if (todayRemain <= 0)
        {
            return false;
        }
        // 先显示当前剩余次数（第一次点击显示8），再扣减
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
            remainText.text = todayRemain.ToString();
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
