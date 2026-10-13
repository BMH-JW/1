using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ClockRotate : MonoBehaviour
{
    public Transform hourHand;
    public Transform minuteHand;

    void Update()
    {
        GameTimeManager timeMgr = GameTimeManager.Instance;
        if (timeMgr == null) return;

        // 分针：1分钟=6度
        float minuteAngle = (timeMgr.currentMinute + timeMgr.currentSecond / 60f) * 6f;
        minuteHand.localEulerAngles = new Vector3(0, 0, -minuteAngle);

        //时针：1小时=30度
        float hourAngle = (timeMgr.currentHour % 12 + timeMgr.currentMinute / 60f) * 30f;
        hourHand.localEulerAngles = new Vector3(0, 0, -hourAngle);
    }
}
