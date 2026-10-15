using System.Collections;
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
    public GameObject stampObj1;
    public GameObject stampObj2;

    [Header("邮箱图片")]
    public GameObject imageMailBox;
    public GameObject imageMailBox2;

    [Header("剩余次数文本物体(拖整个GameObject)")]
    public GameObject remainTextObj;

    private int _lastDay = -1;
    private bool _bellUsedToday;   // 自由审批模式下一天只能点一次铃铛
    private Coroutine _showCoroutine;

    void Start()
    {
        npcObj.SetActive(false);
        Btn_4.SetActive(false);
        Btn_File2.SetActive(false);
        Btn_File3.SetActive(false);
        SetStampsActive(false);
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
            ResetForNewDay();
        }
    }

    /// <summary>新的一天：重置所有状态（铃铛、NPC、按钮、协程）</summary>
    public void ResetForNewDay()
    {
        _bellUsedToday = false;
        if (_showCoroutine != null) { StopCoroutine(_showCoroutine); _showCoroutine = null; }
        npcObj.SetActive(false);
        Btn_4.SetActive(false);
        Btn_File2.SetActive(false);
        Btn_File3.SetActive(false);
        SetStampsActive(false);
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);
        remainTextObj.SetActive(false);
        CheckBellInteractable();
        Debug.Log("[NPCBellLogic] ResetForNewDay 完成");
    }

    private void SetStampsActive(bool active)
    {
        if (stampObj1 != null) stampObj1.SetActive(active);
        if (stampObj2 != null) stampObj2.SetActive(active);
    }

    /// <summary>铃铛点击入口（玩家手动点击），必须绑定到Button的OnClick</summary>
    public void OnClickBell()
    {
        // 自由审批模式下一天只能点一次铃铛
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsFreePlay && _bellUsedToday)
        {
            Debug.Log("[NPCBellLogic] 今天已经摇过铃了，等下一位客人自动到来");
            return;
        }
        _bellUsedToday = true;
        SpawnNpc();
    }

    /// <summary>生成NPC（内部方法，AutoSpawnNextNpc 也调这个，不检查铃铛限制）</summary>
    public void SpawnNpc()
    {
        if (NPCDailyManager.Instance == null) return;

        bool success = NPCDailyManager.Instance.InteractNPC();
        if (!success)
        {
            Debug.Log("次数已用尽，不生成NPC");
            return;
        }

        remainTextObj.SetActive(true);
        npcObj.SetActive(true);
        if (TutorialManager.Instance != null) TutorialManager.Instance.NpcEnter();

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
        SetStampsActive(false);
    }

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
        SetStampsActive(false);
        _showCoroutine = StartCoroutine(NpcShowDelayLogic(0.5f));
    }

    private IEnumerator NpcShowDelayLogic(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (NPCDailyManager.Instance == null || NPCDailyManager.Instance.todayRemain <= 0)
            yield break;
        imageMailBox.SetActive(false);
        imageMailBox2.SetActive(true);
        Btn_4.SetActive(true);
        Btn_4.transform.SetAsLastSibling();
    }

    public void OnClickBtn4()
    {
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);
        Btn_File2.SetActive(true);
        Btn_File3.SetActive(true);
        SetStampsActive(true);
        Debug.Log($"[NPCBellLogic] OnClickBtn4 → 文件+印章激活");

        // 兜底：Stamp 按钮激活后确保 TutorialManager 已挂上 listener（幂等）
        if (TutorialManager.Instance != null) TutorialManager.Instance.TryBindPersistentHooks();
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
