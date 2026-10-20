using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 弹窗拖拽移动
/// 按下弹窗任意空白区域即可拖动；关闭按钮可选，拖入后自动钉在右上角。
/// 注意：同一物体上若挂了 PopupPanel（弹窗基类），关闭按钮位置由 PopupPanel 的槽位控制，
/// 本脚本不再插手，避免两个脚本互相覆盖导致位置怎么调都不变。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DragAndWheelZoomPanel : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    [Header("右上角关闭按钮(仅没有 PopupPanel 的窗口需要；有 PopupPanel 请用它的槽位)")]
    public RectTransform closeButton;
    [Header("关闭按钮距右上角内缩距离 x=距右 y=距上，通常为负")]
    public Vector2 closeButtonOffset = new Vector2(-8f, -8f);

    private RectTransform winRect;
    private RectTransform parentRect;
    private Canvas canvas;
    private PopupPanel _popup;

    // 按下时鼠标与弹窗的偏移量
    private Vector2 moveOffset;

    void Awake()
    {
        winRect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        parentRect = winRect.parent as RectTransform;
        _popup = GetComponent<PopupPanel>();
        SetupCloseButton();
    }

    /// <summary>同物体上挂了 PopupPanel 时，是否可拖动由它的 dragToFront 决定
    /// —— 这样"固定弹窗"只需要在 PopupPanel 里取消勾选拖动置顶，本脚本自动不再拖。</summary>
    private bool CanDrag()
    {
        return _popup == null || _popup.dragToFront;
    }

    private Camera GetEventCamera()
    {
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanDrag()) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, GetEventCamera(), out Vector2 localMouse);
        moveOffset = localMouse - winRect.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!CanDrag()) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, GetEventCamera(), out Vector2 pos);
        winRect.anchoredPosition = pos - moveOffset;
    }

    /// <summary>
    /// 把关闭按钮的锚点钉在其父节点的右上角(单锚点，不拉伸、不变形)，
    /// 弹窗移动时按钮始终保持在右上角。
    /// </summary>
    private void SetupCloseButton()
    {
        if (closeButton == null) return;

        // 同物体上有 PopupPanel 时，关闭按钮位置由 PopupPanel 的槽位统一管理，这里让位
        if (GetComponent<PopupPanel>() != null) return;

        // 记录当前尺寸，避免改锚点后 sizeDelta 语义变化导致按钮变形
        Vector2 size = closeButton.rect.size;

        closeButton.anchorMin = new Vector2(1f, 1f);
        closeButton.anchorMax = new Vector2(1f, 1f);
        closeButton.pivot = new Vector2(1f, 1f);
        closeButton.sizeDelta = size;
        closeButton.anchoredPosition = closeButtonOffset;
    }
}
