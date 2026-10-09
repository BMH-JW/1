using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance;

    [Header("初始时间设置")]
    public int StartDay;
    public int StartHour;
    public int StartMinute;
    public int StartSecond;

    [Header("当前游戏时间（播放模式可直接修改调试）")]
    public int currentDay;
    public int currentHour;
    public int currentMinute;
    public int currentSecond;

    [Header("游戏1分钟 = 真实世界多少秒")]
    public float GameMinuteRealSec = 1f;

    // 事件
    public event System.Action OnNewDay;
    public event System.Action OnWorkStart;   //9点上班
    public event System.Action OnWorkEnd;     //18点下班

    private bool isWorkTimeTriggered = false;
    private float timeAccumulator;

    void Awake()
    {
        Instance = this;
        currentDay = StartDay;
        currentHour = StartHour;
        currentMinute = StartMinute;
        currentSecond = StartSecond;
        timeAccumulator = 0;
        Debug.Log($"【GameTime】初始化完成，currentHour={currentHour}");
    }

    public bool IsWorkTime()
    {
        return currentHour >= 9 && currentHour < 18;
    }

    void Update()
    {
        timeAccumulator += Time.deltaTime;

        // 保护：最小不能低于0.05，避免除以0造成死循环
        float realMinute = Mathf.Max(GameMinuteRealSec, 0.05f);
        float gameSecondDuration = realMinute / 60f;

        // 增加循环最大上限，防止死循环锁死编辑器，最多一帧跑200次
        int loopGuard = 0;
        while (timeAccumulator >= gameSecondDuration && loopGuard < 200)
        {
            loopGuard++;
            timeAccumulator -= gameSecondDuration;
            currentSecond++;

            if (currentSecond >= 60)
            {
                currentSecond = 0;
                currentMinute++;
                if (currentMinute >= 60)
                {
                    currentMinute = 0;
                    currentHour++;
                    if (currentHour >= 24)
                    {
                        currentHour = 0;
                        currentDay++;
                        OnNewDay?.Invoke();
                        isWorkTimeTriggered = false;
                    }
                }
            }
        }

        if (loopGuard >= 200)
        {
            Debug.LogWarning("时间循环超限！检查GameMinuteRealSec数值不要填太小");
        }

        //上班下班事件不变
        if (IsWorkTime())
        {
            if (!isWorkTimeTriggered)
            {
                OnWorkStart?.Invoke();
                isWorkTimeTriggered = true;
            }
        }
        else
        {
            if (isWorkTimeTriggered)
            {
                OnWorkEnd?.Invoke();
                isWorkTimeTriggered = false;
            }
        }
    }
}