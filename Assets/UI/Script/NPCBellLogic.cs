using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class NPCBellLogic : MonoBehaviour
{
    [Header("物体拖拽赋值")]
    public GameObject npcObj;
    public Button Btn_Bell;
    public GameObject Btn_4;
    public GameObject Btn_File1;
    public GameObject Btn_File2;
    public GameObject Btn_File3;

    [Header("邮箱图片")]
    public GameObject imageMailBox;
    public GameObject imageMailBox2;

    private int _lastDay = -1;

    void Start()
    {
        //开局状态：显示MailBox，隐藏MailBox2；其他UI全部隐藏
        npcObj.SetActive(false);
        Btn_4.SetActive(false);
        Btn_File1.SetActive(false);
        Btn_File2.SetActive(false);
        Btn_File3.SetActive(false);

        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);

        if (GameTimeManager.Instance != null)
        {
            _lastDay = GameTimeManager.Instance.currentDay;
        }
        CheckBellInteractable();
    }

    void Update()
    {
        if (GameTimeManager.Instance == null || NPCDailyManager.Instance == null) return;

        int currentDay = GameTimeManager.Instance.currentDay;
        //检测是否到新一天
        if (currentDay != _lastDay)
        {
            _lastDay = currentDay;
            CheckBellInteractable();
        }
    }

    /// <summary>
    /// 铃铛按钮点击
    /// </summary>
    public void OnClickBell()
    {
        if (NPCDailyManager.Instance == null) return;
        if (NPCDailyManager.Instance.todayRemain <= 0) return;

        npcObj.SetActive(true);
        StartCoroutine(NpcShowDelayLogic(1f));
    }

    /// <summary>
    /// Npc出现后等待1秒：显示MailBox2，同时弹出Btn_4
    /// </summary>
    private System.Collections.IEnumerator NpcShowDelayLogic(float delay)
    {
        yield return new WaitForSeconds(delay);

        //切换为MailBox2
        imageMailBox.SetActive(false);
        imageMailBox2.SetActive(true);

        Btn_4.SetActive(true);
    }

    /// <summary>
    /// Btn_4点击
    /// </summary>
    public void OnClickBtn4()
    {
        //点击Btn4，切回MailBox
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);

        Btn_File1.SetActive(true);
        Btn_File2.SetActive(true);
        Btn_File3.SetActive(true);
    }

    /// <summary>
    /// 更新铃铛是否可点击
    /// </summary>
    public void CheckBellInteractable()
    {
        if (NPCDailyManager.Instance == null || Btn_Bell == null) return;
        bool canClick = NPCDailyManager.Instance.todayRemain > 0;
        Btn_Bell.interactable = canClick;
    }
}