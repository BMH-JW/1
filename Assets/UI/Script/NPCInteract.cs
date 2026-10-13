using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCInteract : MonoBehaviour
{
    public void OnClickNPC()
    {
        bool success = NPCDailyManager.Instance.InteractNPC();

        if (success)
        {
            Debug.Log("NPC互动成功，剩余次数减少");
            // 你未来可以在这里加：弹窗、剧情、奖励
        }
        else
        {
            Debug.Log("今日NPC次数已用完 / 非上班时间不可互动");
        }
    }
}