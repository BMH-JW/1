using System.Collections;
using UnityEngine;

public class NPCInteract : MonoBehaviour
{
    private NPCBellLogic _bellLogic;

    /// <summary>
    /// NPC 被玩家点击。
    /// 注意：扣次数由 NPCBellLogic.OnClickBell 统一负责，这里只做流程推进。
    /// 点过 mailbox / 进入资料阶段后，npcLocked 会把点击拦掉，防止跳过审批直接离场。
    /// </summary>
    public void OnClickNPC()
    {
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsNpcLocked)
        {
            Debug.Log("npcLocked：资料已打开，不能再点 NPC 跳过");
            return;
        }

        if (_bellLogic == null) _bellLogic = FindObjectOfType<NPCBellLogic>();
        if (_bellLogic != null)
        {
            _bellLogic.RefreshMailBox();
            if (TutorialManager.Instance != null) TutorialManager.Instance.LockNpc();
        }
    }
}
