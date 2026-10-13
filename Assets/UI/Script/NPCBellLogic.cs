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
    public GameObject Btn_File2;
    public GameObject Btn_File3;

    [Header("邮箱图片")]
    public GameObject imageMailBox;
    public GameObject imageMailBox2;

    [Header("剩余次数文本物体(拖整个GameObject)")]
    public GameObject remainTextObj;

    private int _lastDay = -1;
    private Coroutine _showCoroutine;

    void Start()
    {
        npcObj.SetActive(false);
        Btn_4.SetActive(false);
        Btn_File2.SetActive(false);
        Btn_File3.SetActive(false);
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);

        remainTextObj.SetActive(false);
        EnsureMailBox2Clickable();

        if (GameTimeManager.Instance != null)
        {
            _lastDay = GameTimeManager.Instance.currentDay;
        }
        StartCoroutine(DelayRefreshBell());
    }

    IEnumerator DelayRefreshBell()
    {
        yield return null;
        CheckBellInteractable();
    }

    void Update()
    {
        if (GameTimeManager.Instance == null || NPCDailyManager.Instance == null) return;
        int currentDay = GameTimeManager.Instance.currentDay;

        if (currentDay != _lastDay)
        {
            _lastDay = currentDay;
            CheckBellInteractable();
            remainTextObj.SetActive(false);
        }
    }

    /// <summary>铃铛点击入口，必须绑定到Button的OnClick</summary>
    public void OnClickBell()
    {
        if (NPCDailyManager.Instance == null) return;

        bool success = NPCDailyManager.Instance.InteractNPC();
        if (!success)
        {
            Debug.Log("次数已用尽，不生成NPC");
            return;
        }

        // 扣减后归0，隐藏所有NPC相关物体，只保留剩余次数显示
        if (NPCDailyManager.Instance.todayRemain <= 0)
        {
            remainTextObj.SetActive(true);
            npcObj.SetActive(false);
            Btn_4.SetActive(false);
            Btn_File2.SetActive(false);
            Btn_File3.SetActive(false);
            imageMailBox.SetActive(true);
            imageMailBox2.SetActive(false);
            return;
        }

        remainTextObj.SetActive(true);
        npcObj.SetActive(true);
        // 铃铛只负责生成NPC，邮箱回到初始状态，等点击NPC后刷新
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);
        Btn_4.SetActive(false);
        Btn_File2.SetActive(false);
        Btn_File3.SetActive(false);
    }

    /// <summary>点击NPC时调用：刷新一次邮箱（先回初始状态，再切换到新邮件状态）</summary>
    public void RefreshMailBox()
    {
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);
        Btn_4.SetActive(false);
        Btn_File2.SetActive(false);
        Btn_File3.SetActive(false);
        _showCoroutine = StartCoroutine(NpcShowDelayLogic(0.5f));
    }

    private System.Collections.IEnumerator NpcShowDelayLogic(float delay)
    {
        yield return new WaitForSeconds(delay);
        // 如果次数已归0，不执行后续显示逻辑
        if (NPCDailyManager.Instance == null || NPCDailyManager.Instance.todayRemain <= 0)
            yield break;
        imageMailBox.SetActive(false);
        imageMailBox2.SetActive(true);
        Btn_4.SetActive(true);
        // 保证按钮在最上层，避免被邮箱图片挡住射线导致点不到
        Btn_4.transform.SetAsLastSibling();
    }

    public void OnClickBtn4()
    {
        // 停掉未完成的显示协程，防止状态被翻转回去（点了没反应的主要原因）
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);
        Btn_File2.SetActive(true);
        Btn_File3.SetActive(true);
    }

    /// <summary>
    /// 保证点击邮箱图片本身(新旧两张)也能触发OnClickBtn4，
    /// 即使Btn_4被邮箱图片挡住、或在1秒切换延迟内点击，也能一次出现文件按钮。
    /// </summary>
    private void EnsureMailBox2Clickable()
    {
        BindMailBoxClick(imageMailBox);
        BindMailBoxClick(imageMailBox2);
    }

    private void BindMailBoxClick(GameObject mailBox)
    {
        if (mailBox == null) return;

        Image img = mailBox.GetComponent<Image>();
        if (img == null) img = mailBox.GetComponentInChildren<Image>();
        if (img == null) return;

        img.raycastTarget = true;
        Button btn = img.GetComponent<Button>();
        if (btn == null) btn = img.gameObject.AddComponent<Button>();
        btn.onClick.AddListener(OnClickBtn4);
    }

    public void CheckBellInteractable()
    {
        if (NPCDailyManager.Instance == null || Btn_Bell == null) return;
        bool canClick = NPCDailyManager.Instance.todayRemain > 0;
        Btn_Bell.interactable = canClick;
        Debug.Log($"铃铛可点击状态：{canClick}  todayRemain={NPCDailyManager.Instance.todayRemain}");
    }
}