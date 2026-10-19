using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JU.GameData;

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

    [Header("NPC随机池(拖6个NPC预制体，每次摇铃随机出一个；留空则用上方单个npcObj)")]
    public GameObject[] npcPrefabs;

    // 洗牌袋：打乱顺序依次取，6个全出过一遍后才重新洗，避免连续重复同一个NPC
    private readonly List<GameObject> _shuffleBag = new List<GameObject>();
    private GameObject _originalNpc;   // 场景里原始的npcObj（位置锚点）
    private GameObject _spawnedNpc;    // 当前由随机池实例化出来的NPC
    private Vector3 _anchorPos;
    private Quaternion _anchorRot;
    private Vector3 _anchorScale;
    private bool _anchorCached;

    // GameSession 接入：当前 NPC 的 session 数据 + 日级索引
    public NpcData currentNpcData;         // 当前正在审批的 NPC 数据（给 RoomInteract/TutorialManager 用）
    public static NPCBellLogic Instance { get; private set; }
    private int _todayNpcIndex;             // 今天已摇铃的序号（从 GameSession state.npcs 按顺序取）

    [Header("邮箱图片")]
    public GameObject imageMailBox;
    public GameObject imageMailBox2;

    [Header("剩余次数文本物体(拖整个GameObject)")]
    public GameObject remainTextObj;

    private int _lastDay = -1;
    private bool _npcPresent;     // 场上是否有NPC（有则铃铛不可再点）
    private Coroutine _showCoroutine;

    void Start()
    {
        Instance = this;
        _originalNpc = npcObj;
        npcObj.SetActive(false);
        Btn_4.SetActive(false);
        Btn_File2.SetActive(false);
        Btn_File3.SetActive(false);
        SetStampsActive(true);   // 印章常驻显示
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);

        remainTextObj.SetActive(true); // 剩余人数文本常驻显示
        EnsureMailBox2Clickable();
        SetMailboxClickable(false); // NPC没来之前邮箱不可点（必须在EnsureMailBox2Clickable之后，避免后补的Button重置为可点）

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

    /// <summary>新的一天：重置所有状态（NPC、按钮、协程、日级索引）。剩余人数文本和印章常驻不动</summary>
    public void ResetForNewDay()
    {
        _npcPresent = false;
        _todayNpcIndex = 0;
        currentNpcData = null;
        if (_showCoroutine != null) { StopCoroutine(_showCoroutine); _showCoroutine = null; }
        DespawnRandomNpc(); // 销毁随机池实例并把npcObj还原为原始NPC
        npcObj.SetActive(false);
        Btn_4.SetActive(false);
        Btn_File2.SetActive(false);
        Btn_File3.SetActive(false);
        SetStampsActive(true);
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);
        // remainTextObj 常驻，不隐藏
        SetMailboxClickable(false); // 新一天NPC还没来，邮箱锁住
        CheckBellInteractable();
        Debug.Log("[NPCBellLogic] ResetForNewDay 完成");
    }

    private void SetStampsActive(bool active)
    {
        if (stampObj1 != null) stampObj1.SetActive(active);
        if (stampObj2 != null) stampObj2.SetActive(active);
    }

    /// <summary>设置两张邮箱图是否可点击（NPC没来之前不可点）</summary>
    private void SetMailboxClickable(bool clickable)
    {
        SetOneMailboxClickable(imageMailBox, clickable);
        SetOneMailboxClickable(imageMailBox2, clickable);
    }

    private void SetOneMailboxClickable(GameObject mailBox, bool clickable)
    {
        if (mailBox == null) return;
        Button btn = mailBox.GetComponent<Button>();
        if (btn == null) btn = mailBox.GetComponentInChildren<Button>();
        if (btn != null) btn.interactable = clickable;
    }

    /// <summary>铃铛点击入口（玩家手动点击），必须绑定到Button的OnClick</summary>
    public void OnClickBell()
    {
        // 场上已有NPC（资料已出来 / NPC还没走），不允许再摇铃
        if (_npcPresent)
        {
            Debug.Log("[NPCBellLogic] 当前客人还没离开，不能摇铃叫下一位");
            return;
        }
        SpawnNpc();
    }

    /// <summary>生成NPC（摇铃调用）。一次只出一个NPC，退场后需要玩家再次摇铃。
    /// 配置了随机池时每次随机实例化一个NPC，否则用场景里固定的npcObj</summary>
    public void SpawnNpc()
    {
        if (NPCDailyManager.Instance == null) return;

        bool success = NPCDailyManager.Instance.InteractNPC();
        if (!success)
        {
            Debug.Log("次数已用尽，不生成NPC");
            return;
        }

        _npcPresent = true;
        CheckBellInteractable(); // 立刻锁铃铛，资料出来前后都不能再点
        SetMailboxClickable(true); // NPC来了，邮箱可以点了

        remainTextObj.SetActive(true);
        SpawnRandomNpc();
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
        // 印章常驻，不隐藏
    }

    /// <summary>从随机池实例化一个NPC到原npcObj的位置，并把npcObj指向它（下游TutorialManager无需改动）。
    /// GameSession 存在时，同步按日级索引从 state.npcs 取 NpcData 存 currentNpcData</summary>
    private void SpawnRandomNpc()
    {
        // 先从 GameSession 取下一个 NPC 数据（按索引，每天从 0 开始）
        TryLoadNextGameSessionNpc();

        if (npcPrefabs == null || npcPrefabs.Length == 0) return; // 没配池子，用固定npcObj

        GameObject prefab = DrawFromShuffleBag();
        if (prefab == null) return;

        // 第一次生成时缓存原npcObj的摆位作为出生锚点
        if (!_anchorCached && _originalNpc != null)
        {
            _anchorPos = _originalNpc.transform.localPosition;
            _anchorRot = _originalNpc.transform.localRotation;
            _anchorScale = _originalNpc.transform.localScale;
            _anchorCached = true;
        }

        // 销毁上一个实例（其退场动画已播完才会走到这里）
        if (_spawnedNpc != null) Destroy(_spawnedNpc);

        Transform parent = _originalNpc != null ? _originalNpc.transform.parent : null;
        GameObject npc = parent != null ? Instantiate(prefab, parent) : Instantiate(prefab);
        // 用 GameSession 的显示名（如果有）替换 prefab 名
        npc.name = currentNpcData != null ? currentNpcData.name : prefab.name;
        if (parent != null)
        {
            npc.transform.localPosition = _anchorPos;
            npc.transform.localRotation = _anchorRot;
            npc.transform.localScale = _anchorScale;
        }
        npc.SetActive(false);

        npcObj = npc;
        _spawnedNpc = npc;
        Debug.Log($"[NPCBellLogic] 出场NPC：{npc.name} (id={currentNpcData?.id}, identityId={currentNpcData?.identityId}, realOrigin={currentNpcData?.realOrigin})");
    }

    /// <summary>从 GameSession 今日的 npcs 队列中按下一个索引取 NpcData；
    /// 越界 / Bridge 不存在 → 保持 currentNpcData 为 null（RoomInteract 会 fallback）</summary>
    private void TryLoadNextGameSessionNpc()
    {
        var bridge = GameSessionBridge.Instance;
        if (bridge == null || bridge.Current == null) { currentNpcData = null; return; }

        var state = bridge.Current.GetState();
        if (state.npcs == null || state.npcs.Count == 0) { currentNpcData = null; return; }

        if (_todayNpcIndex >= state.npcs.Count)
        {
            // 越界：GameSession 今天的申请者已经都过完了（不应该发生，因为 NPCDailyManager 也按 todayApplicantCount 限制）
            Debug.LogWarning($"[NPCBellLogic] _todayNpcIndex={_todayNpcIndex} >= state.npcs.Count={state.npcs.Count}，不再取 session NPC");
            currentNpcData = null;
            return;
        }

        currentNpcData = state.npcs[_todayNpcIndex];
        _todayNpcIndex++;
    }

    /// <summary>洗牌袋取预制体：打乱后依次取，取空重新洗，保证不连续重复</summary>
    private GameObject DrawFromShuffleBag()
    {
        if (_shuffleBag.Count == 0)
        {
            foreach (GameObject p in npcPrefabs)
            {
                if (p != null) _shuffleBag.Add(p);
            }
            if (_shuffleBag.Count == 0)
            {
                Debug.LogWarning("[NPCBellLogic] npcPrefabs 里没有有效预制体");
                return null;
            }
            // Fisher-Yates 洗牌
            for (int i = _shuffleBag.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                GameObject tmp = _shuffleBag[i];
                _shuffleBag[i] = _shuffleBag[j];
                _shuffleBag[j] = tmp;
            }
        }
        GameObject pick = _shuffleBag[0];
        _shuffleBag.RemoveAt(0);
        return pick;
    }

    /// <summary>销毁随机池生成的当前NPC实例，npcObj 还原为场景原始NPC</summary>
    private void DespawnRandomNpc()
    {
        if (_spawnedNpc != null)
        {
            Destroy(_spawnedNpc);
            _spawnedNpc = null;
        }
        if (npcObj != _originalNpc) npcObj = _originalNpc;
    }

    /// <summary>立即隐藏档案/通关文书按钮和Btn_4（教程结束时调用，不等NPC退场动画）</summary>
    public void HideFileButtons()
    {
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }
        if (Btn_4 != null) Btn_4.SetActive(false);
        if (Btn_File2 != null) Btn_File2.SetActive(false);
        if (Btn_File3 != null) Btn_File3.SetActive(false);
        Debug.Log("[NPCBellLogic] HideFileButtons → Btn_File2/Btn_File3/Btn_4 已隐藏");
    }

    /// <summary>NPC退场收尾：恢复桌面初始状态、锁定邮箱与铃铛，等玩家再次手动摇铃</summary>
    public void OnNpcLeft()
    {
        _npcPresent = false;
        HideFileButtons();
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);
        SetMailboxClickable(false); // NPC走了，邮箱重新锁住
        // 印章常驻，不隐藏；剩余人数文本常驻，不隐藏
        CheckBellInteractable();
        Debug.Log("[NPCBellLogic] NPC已离场，等待玩家再次摇铃");
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
        // 权威拦截：NPC没来之前点邮箱无效（双保险，即使Button状态被别处改动）
        if (!_npcPresent)
        {
            Debug.Log("[NPCBellLogic] 客人还没来，不能查看邮箱");
            return;
        }
        if (_showCoroutine != null)
        {
            StopCoroutine(_showCoroutine);
            _showCoroutine = null;
        }
        imageMailBox.SetActive(true);
        imageMailBox2.SetActive(false);
        Btn_File2.SetActive(true);
        Btn_File3.SetActive(true);
        // 印章本身常驻，这里只确保是激活状态
        SetStampsActive(true);
        // 人物资料出来后铃铛不可点
        if (Btn_Bell != null) Btn_Bell.interactable = false;
        Debug.Log("[NPCBellLogic] OnClickBtn4 → 文件+印章激活，铃铛已锁定");

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
        // 有次数 且 场上没有NPC时，铃铛才可点
        bool canClick = NPCDailyManager.Instance.todayRemain > 0 && !_npcPresent;
        Btn_Bell.interactable = canClick;
        Debug.Log($"铃铛可点击状态：{canClick}  todayRemain={NPCDailyManager.Instance.todayRemain}  npcPresent={_npcPresent}");
    }
}
