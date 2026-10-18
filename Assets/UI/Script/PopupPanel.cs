using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 弹窗基面板：文本/关闭 + 拖动置顶。
/// 拖动弹窗任意空白处（或从按钮上开始拖）即可移动弹窗，
/// 拖动/点击的弹窗自动置顶（SetAsLastSibling），实现"谁被操作谁在最上层"。
/// </summary>
public class PopupPanel : MonoBehaviour, IBeginDragHandler, IDragHandler, IPointerDownHandler
{
    /// <summary>来源标记：File3=通关文书弹窗，File2=档案弹窗，空=其他</summary>
    public string popupSource = "";

    public TMP_Text titleText;
    public TMP_Text contentText;
    public Button closeBtn;

    [Header("拖动置顶")]
    [Tooltip("勾选后：按住弹窗拖动可移动位置，被拖动/点击的弹窗自动置顶")]
    public bool dragToFront = true;

    private RectTransform _rt;
    private float _canvasScale = 1f;

    void Awake()
    {
        if (closeBtn != null) closeBtn.onClick.AddListener(ClosePanel);
        _rt = (RectTransform)transform;
        Canvas c = GetComponentInParent<Canvas>();
        if (c != null) _canvasScale = c.rootCanvas.scaleFactor; // Canvas缩放校正，保证拖动跟手
    }

    public void SetSource(string source)
    {
        popupSource = source;
    }

    public void SetText(string title, string content)
    {
        if (titleText != null) titleText.text = title;
        if (contentText != null) contentText.text = content;
    }

    public void ClosePanel()
    {
        RoomInteract.Untrack(gameObject);
        Destroy(gameObject);
    }

    // ================================================================
    // 拖动置顶：点在空白处也置顶；从按钮上开始拖会冒泡到这里
    // ================================================================
    public void OnPointerDown(PointerEventData eventData)
    {
        if (dragToFront) _rt.SetAsLastSibling();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (dragToFront) _rt.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!dragToFront) return;
        _rt.anchoredPosition += eventData.delta / _canvasScale;
    }
}
