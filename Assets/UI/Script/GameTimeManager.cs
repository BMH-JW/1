using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class GameTimeManager : MonoBehaviour
{
    public static GameTimeManager Instance;

    [Header("��ʼʱ������")]
    public int StartDay;
    public int StartHour;
    public int StartMinute;
    public int StartSecond;

    [Header("��ǰ��Ϸʱ�䣨����ģʽ��ֱ���޸ĵ��ԣ�")]
    public int currentDay;
    public int currentHour;
    public int currentMinute;
    public int currentSecond;

    [Header("��Ϸ1���� = ��ʵ���������")]
    public float GameMinuteRealSec = 1f;

    // �¼�
    public event System.Action OnNewDay;
    public event System.Action OnWorkStart;   //9���ϰ�
    public event System.Action OnWorkEnd;     //18���°�

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
        Debug.Log($"��GameTime����ʼ����ɣ�currentHour={currentHour}");
    }

    public bool IsWorkTime()
    {
        return currentHour >= 9 && currentHour < 18;
    }

    /// <summary>直接跳到第二天 9:00（结算黑屏过渡时调用）</summary>
    public void JumpToNextWorkday()
    {
        currentDay++;
        currentHour = 9;
        currentMinute = 0;
        currentSecond = 0;
        timeAccumulator = 0f;
        isWorkTimeTriggered = false;
        Debug.Log($"[GameTime] 跳到第 {currentDay} 天 9:00");
        OnNewDay?.Invoke();
        OnWorkStart?.Invoke();
        isWorkTimeTriggered = true;
    }

    void Update()
    {
        timeAccumulator += Time.deltaTime;

        // ��������С���ܵ���0.05���������0�����ѭ��
        float realMinute = Mathf.Max(GameMinuteRealSec, 0.05f);
        float gameSecondDuration = realMinute / 60f;

        // ����ѭ��������ޣ���ֹ��ѭ�������༭�������һ֡��200��
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
            Debug.LogWarning("ʱ��ѭ�����ޣ����GameMinuteRealSec��ֵ��Ҫ��̫С");
        }

        //�ϰ��°��¼�����
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