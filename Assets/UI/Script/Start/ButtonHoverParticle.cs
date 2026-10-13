using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(UnityEngine.UI.Button))]
public class ButtonHoverParticle : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("悬浮粒子")]
    public ParticleSystem hoverParticle;

    void Start()
    {
        if (hoverParticle != null)
        {
            //游戏一开始停止并且清除所有粒子
            hoverParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    //鼠标移动到按钮上时执行
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (hoverParticle == null) return;
        hoverParticle.Play();
    }

    //鼠标从按钮移开时执行
    public void OnPointerExit(PointerEventData eventData)
    {
        if (hoverParticle == null) return;
        hoverParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}