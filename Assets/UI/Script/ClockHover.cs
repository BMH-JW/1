using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class ClockHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    //改成GameObject，Hierarchy直接拖ClockRoot物体进去
    public GameObject clockRootObj;
    private RectTransform clockRoot;

    public Vector2 originPos;
    public Vector2 originSize;
    public Vector2 targetPos;
    public Vector2 targetSize;
    public float lerpSpeed = 8f;

    private bool isHover;

    void Awake()
    {
        //自动获取RectTransform
        clockRoot = clockRootObj.GetComponent<RectTransform>();
    }

    void Update()
    {
        if (clockRoot == null) return;
        if (isHover)
        {
            clockRoot.anchoredPosition = Vector2.Lerp(clockRoot.anchoredPosition, targetPos, Time.deltaTime * lerpSpeed);
            clockRoot.sizeDelta = Vector2.Lerp(clockRoot.sizeDelta, targetSize, Time.deltaTime * lerpSpeed);
        }
        else
        {
            clockRoot.anchoredPosition = Vector2.Lerp(clockRoot.anchoredPosition, originPos, Time.deltaTime * lerpSpeed);
            clockRoot.sizeDelta = Vector2.Lerp(clockRoot.sizeDelta, originSize, Time.deltaTime * lerpSpeed);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => isHover = true;
    public void OnPointerExit(PointerEventData eventData) => isHover = false;
}
