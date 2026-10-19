using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using JU.GameData;

/// <summary>弹窗管理器（多实例共存，通过 popupSource 区分来源）</summary>
public class RoomInteract : MonoBehaviour
{
    /// <summary>标记这个按钮的弹窗是啥（Rules=规则公告 / File3=通关文书 / File2=档案 / ...），TutorialManager 盖章时只找 File3</summary>
    public string popupSource = "";

    public GameObject popupPrefab;
    public string title;
    [TextArea(5, 10)] public string content;

    /// <summary>content 支持的占位符：
    ///   {RULES} → 从 GameSessionBridge 取 session.GetRuleAnnouncement()（每日规则公告）
    ///   以后可以扩 {DOC} {PROFILE} 等 per-NPC 动态内容
    /// </summary>
    private static readonly string RulesPlaceholder = "{RULES}";

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
            panel.SetText(title, ResolveContent());

            // 通关文书 / 档案：按字段填入 documentFields（popupSource 在 prefab 里配好）
            if (!string.IsNullOrEmpty(popupSource))
            {
                var fieldValues = ResolveDocumentFields(popupSource);
                Debug.Log($"[RoomInteract] ResolveDocumentFields({popupSource}) → {(fieldValues == null ? "null" : $"{fieldValues.Count} 字段: [{string.Join(", ", fieldValues.Keys)}]")}");
                if (fieldValues != null && fieldValues.Count > 0)
                    panel.SetDocumentFields(fieldValues);
            }
        }
        Debug.Log($"[RoomInteract] OpenPopup source={popupSource} → tracked={_myPopup.name} (trackedCount={_trackedPopups.Count})");
    }

    /// <summary>按 popupSource 取数据对象的原生字段字典 —— 就是 GameSession 里 GetDocument / GetProfile 返回的那些属性，不加任何拼接/格式化。
    /// key 就是 inspector 里 documentFields 要填的 fieldName，value 直接是数据模型的属性值。
    /// 没有数据就返回 null（弹窗 title+content 仍正常显示）。</summary>
    private Dictionary<string, string> ResolveDocumentFields(string source)
    {
        var bridge = GameSessionBridge.Instance;
        var bell = NPCBellLogic.Instance;
        if (bridge == null || bridge.Current == null)
        {
            Debug.LogWarning($"[RoomInteract] ResolveDocumentFields({source}) —— bridge.Current 拿不到，返回 null");
            return null;
        }
        if (bell == null || bell.currentNpcData == null)
        {
            Debug.LogWarning($"[RoomInteract] ResolveDocumentFields({source}) —— currentNpcData 拿不到，返回 null");
            return null;
        }

        var npc = bell.currentNpcData;
        var dict = new Dictionary<string, string>();

        switch (source)
        {
            case "File3":  // 通关文书 —— TravelDocument 原生字段
            {
                var doc = bridge.Current.GetDocument(npc.id);
                if (doc == null) { Debug.LogWarning($"[RoomInteract] GetDocument({npc.id}) 返回 null"); return null; }

                var profile = bridge.Current.GetProfile(npc.id);

                // 两套 key：直觉别名（origin/destination/reason）+ 数据模型属性名（bodyText/reasonText/...）
                dict["name"]                = doc.name;
                dict["origin"]              = doc.sourceDisplayName;
                dict["declaredOrigin"]      = doc.declaredOrigin;
                dict["source"]              = doc.sourceDisplayName;
                dict["destination"]         = doc.destinationDisplayName;
                dict["destinationDisplayName"] = doc.destinationDisplayName;
                dict["bodyText"]            = doc.bodyText;
                dict["reason"]              = doc.reasonText;       // 直觉别名
                dict["reasonText"]          = doc.reasonText;
                dict["health"]              = profile?.bodyText ?? "";  // 健康状态从 Profile 取
                break;
            }

            case "File2":  // 档案 —— NpcProfile 原生字段
            {
                var profile = bridge.Current.GetProfile(npc.id);
                if (profile == null) { Debug.LogWarning($"[RoomInteract] GetProfile({npc.id}) 返回 null"); return null; }

                dict["name"]           = profile.name;
                dict["origin"]         = profile.realOrigin;       // 直觉别名
                dict["realOrigin"]     = profile.realOrigin;
                dict["motive"]         = profile.motive;
                dict["memoryText"]     = profile.memoryText;
                dict["relationshipText"]= profile.relationshipText;
                dict["desireText"]     = profile.desireText;
                dict["willText"]       = profile.willText;
                dict["bodyText"]       = profile.bodyText;

                if (profile.bondTag != null)   dict["bondTag"]   = profile.bondTag.description;
                if (profile.driveTag != null)  dict["driveTag"]  = profile.driveTag.description;
                break;
            }

            default:
                return null;
        }

        return dict;
    }

    /// <summary>把 content 里的占位符替换成 GameSessionBridge / NPCBellLogic 的动态数据；没有 Bridge 就原样返回（不崩）
    /// 支持：
    ///   {RULES}      = 今日规则公告 (session.GetRuleAnnouncement)
    ///   {DOC}        = 当前 NPC 的通关文书正文 + 理由 (session.GetDocument(id).bodyText + reasonText)
    ///   {NPC_NAME}   = 当前 NPC 显示名 (NpcData.name)
    ///   {NPC_ORIGIN} = 当前 NPC 申报来源 (NpcData.declaredOrigin ?? realOrigin)
    ///   {NPC_ID}     = 当前 NPC session id (NpcData.id) — 给 SubmitApplication 用</summary>
    private string ResolveContent()
    {
        string resolved = content ?? "";
        var bridge = GameSessionBridge.Instance;
        var bell = NPCBellLogic.Instance;
        bool hasBridge = bridge != null && bridge.Current != null;
        bool hasNpc = bell != null && bell.currentNpcData != null;

        // 1. 全局规则
        if (hasBridge && resolved.Contains(RulesPlaceholder))
            resolved = resolved.Replace(RulesPlaceholder, bridge.Current.GetRuleAnnouncement());

        // 2. 当前 NPC 字段
        if (hasNpc)
        {
            var npc = bell.currentNpcData;

            if (resolved.Contains("{NPC_NAME}"))
                resolved = resolved.Replace("{NPC_NAME}", npc.name ?? "未知");

            if (resolved.Contains("{NPC_ORIGIN}"))
                resolved = resolved.Replace("{NPC_ORIGIN}", npc.declaredOrigin ?? npc.realOrigin ?? "—");

            if (resolved.Contains("{NPC_ID}"))
                resolved = resolved.Replace("{NPC_ID}", npc.id ?? "");

            // 通关文书：从 session.GetDocument(id) 取 bodyText + reasonText
            if (resolved.Contains("{DOC}") && hasBridge)
            {
                var doc = bridge.Current.GetDocument(npc.id);
                string docText = "";
                if (doc != null)
                {
                    docText += string.IsNullOrEmpty(doc.declaredOrigin) ? "" : $"申报来源：{doc.declaredOrigin}\n";
                    if (!string.IsNullOrEmpty(doc.sourceDisplayName) || !string.IsNullOrEmpty(doc.destinationDisplayName))
                        docText += $"行程：{doc.sourceDisplayName ?? ""} → {doc.destinationDisplayName ?? ""}\n";
                    docText += "\n";
                    docText += string.IsNullOrEmpty(doc.bodyText) ? "" : $"{doc.bodyText}\n";
                    if (!string.IsNullOrEmpty(doc.reasonText))
                        docText += $"\n———\n{doc.reasonText}";
                }
                resolved = resolved.Replace("{DOC}", string.IsNullOrEmpty(docText) ? "（无通关文书）" : docText);
            }
        }

        return resolved;
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
