using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CalendarText : MonoBehaviour
{
    public Text dateText;

    void Start()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnNewDay += UpdateDate;
        }
        UpdateDate();
    }

    void Update()
    {
        //实时刷新，Inspector改currentDay时文字立刻同步，调试用
        UpdateDate();
    }

    void UpdateDate()
    {
        if (GameTimeManager.Instance == null) return;
        dateText.text = "第" + GameTimeManager.Instance.currentDay + "天";
    }

    void OnDestroy()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnNewDay -= UpdateDate;
        }
    }
}