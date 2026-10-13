using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NPCInteract : MonoBehaviour
{
    private NPCBellLogic _bellLogic;

    public void OnClickNPC()
    {
        bool success = NPCDailyManager.Instance.InteractNPC();

        if (success)
        {
            Debug.Log("NPC交互成功，剩余次数已扣减");
            if (_bellLogic == null) _bellLogic = FindObjectOfType<NPCBellLogic>();
            // 每点一次NPC刷新一次邮箱
            if (_bellLogic != null) _bellLogic.RefreshMailBox();
            // 归0时隐藏所有NPC相关物体
            if (NPCDailyManager.Instance.todayRemain <= 0)
            {
                HideAllNPCParts();
            }
        }
        else
        {
            Debug.Log("无法NPC交互：次数用尽 / 非上班时间不可交互");
        }
    }

    private void HideAllNPCParts()
    {
        NPCBellLogic bellLogic = FindObjectOfType<NPCBellLogic>();
        if (bellLogic == null) return;

        if (bellLogic.npcObj != null) bellLogic.npcObj.SetActive(false);
        if (bellLogic.Btn_4 != null) bellLogic.Btn_4.SetActive(false);
        if (bellLogic.Btn_File2 != null) bellLogic.Btn_File2.SetActive(false);
        if (bellLogic.Btn_File3 != null) bellLogic.Btn_File3.SetActive(false);
        if (bellLogic.imageMailBox != null) bellLogic.imageMailBox.SetActive(true);
        if (bellLogic.imageMailBox2 != null) bellLogic.imageMailBox2.SetActive(false);
    }
}
