using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>弹窗管理器（多实例共存，通过 popupSource 区分来源）</summary>
public class RoomInteract : MonoBehaviour
{
    /// <summary>标记这个按钮的弹窗是啥（File3=通关文书 / File2=档案 / ...），TutorialManager 盖章时只找 File3</summary>
    public string popupSource = "";

    public GameObject popupPrefab;
    public string title;
    [TextArea(5, 10)] public string content;

    private GameObject _myPopup;

    /// <summary>全局追踪所有 RoomInteract 创建的弹窗（NPC 退场时只 Destroy 这些）</summary>
    private static readonly HashSet<GameObject> _trackedPopups = new HashSet<GameObject>();

    public static IReadOnlyCollection<GameObject> TrackedPopups => _trackedPopups;

    void Start()
    {
        Button btn = GetComponent<Button>();
        if (btn != null) btn.onClick.AddListener(OpenPopup);
    }

    public void OpenPopup()
    {
        // 关掉自己之前弹的那个（不影响别的按钮的弹窗）
        if (_myPopup != null)
        {
            _trackedPopups.Remove(_myPopup);
            Destroy(_myPopup);
        }

        // Instantiate 到 Canvas 下
        GameObject canvasGO = GameObject.Find("Canvas");
        Transform parent = canvasGO != null ? canvasGO.transform : transform.parent.parent;
        _myPopup = Instantiate(popupPrefab, parent);
        _trackedPopups.Add(_myPopup);

        // 标记来源，TutorialManager 靠这个找 File3 的弹窗
        PopupPanel panel = _myPopup.GetComponent<PopupPanel>();
        if (panel != null)
        {
            panel.SetSource(popupSource);
            panel.SetText(title, content);
        }
        Debug.Log($"[RoomInteract] OpenPopup source={popupSource} → tracked={_myPopup.name} (trackedCount={_trackedPopups.Count})");
    }

    /// <summary>关掉本按钮弹出的弹窗</summary>
    public void CloseMyPopup()
    {
        if (_myPopup != null)
        {
            _trackedPopups.Remove(_myPopup);
            Debug.Log($"[RoomInteract] CloseMyPopup Destroying {_myPopup.name}");
            Destroy(_myPopup);
            _myPopup = null;
        }
    }

    /// <summary>关掉所有弹窗（追踪集合 + 场景兜底扫描，双保险）</summary>
    public static void CloseAllPopups()
    {
        // 1. 销毁追踪集合里的弹窗
        var toDestroy = new List<GameObject>(_trackedPopups);
        _trackedPopups.Clear();
        foreach (var go in toDestroy)
        {
            if (go != null)
            {
                Debug.Log($"[RoomInteract] CloseAllPopups Destroying tracked popup: {go.name}");
                Destroy(go);
            }
        }

        // 2. 兜底：扫场景里所有激活的 PopupPanel（防止追踪集合漏网）
        foreach (var p in FindObjectsOfType<PopupPanel>())
        {
            if (p != null && p.gameObject.scene.IsValid() && p.gameObject.activeInHierarchy)
            {
                Debug.Log($"[RoomInteract] CloseAllPopups 兜底销毁: {p.gameObject.name}");
                Destroy(p.gameObject);
            }
        }
    }

    /// <summary>PopupPanel 被玩家手动关掉时（点关闭按钮），从追踪集合里移除</summary>
    public static void Untrack(GameObject popup)
    {
        if (popup != null)
        {
            _trackedPopups.Remove(popup);
            Debug.Log($"[RoomInteract] Untrack {popup.name} (trackedCount={_trackedPopups.Count})");
        }
    }
}
