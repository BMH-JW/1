using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 八方向拖拽缩放 + 整体移动
/// 思路：按下时记录窗口在父节点中的四条边，拖拽时只修改被抓住的那条边，
///       到达极值时把该边锁住、对边不动，最后通过 offsetMin/offsetMax 写回。
///       这种做法与 pivot / anchor 设置无关，任意 Canvas 模式下都准确。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DragAndWheelZoomPanel : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler,
    IPointerMoveHandler, IPointerExitHandler
{
    [Header("边框识别厚度(px) 推荐10~14")]
    public float borderThickness = 12f;
    [Header("最小尺寸")]
    public Vector2 minSize = new Vector2(200, 150);
    [Header("最大尺寸")]
    public Vector2 maxSize = new Vector2(900, 700);
    [Header("是否启用鼠标样式变化")]
    public bool enableCursor = true;
    [Header("光标颜色")]
    public Color cursorColor = Color.white;

    [Header("右上角关闭按钮(拖入后自动钉在右上角)")]
    public RectTransform closeButton;
    [Header("关闭按钮距右上角内缩距离 x=距右 y=距上，通常为负")]
    public Vector2 closeButtonOffset = new Vector2(-8f, -8f);
    [Header("按钮最外侧一条让出给窗口，保证角上能缩放")]
    public bool keepResizeCornerClickable = true;

    private RectTransform winRect;
    private RectTransform parentRect;
    private Canvas canvas;

    private enum DragRegion
    {
        None, Move,
        Left, Right, Top, Bottom,
        LeftTop, RightTop, LeftBottom, RightBottom
    }
    private DragRegion dragRegion = DragRegion.None;

    // 按下时鼠标在父节点中的坐标
    private Vector2 startMouseLocal;
    // 按下时窗口四边在父节点中的坐标
    private float startLeft, startRight, startTop, startBottom;
    // 整体移动用的偏移
    private Vector2 moveOffset;

    // 光标贴图（运行时生成，无需外部资源）
    private Texture2D curHorizontal;
    private Texture2D curVertical;
    private Texture2D curDiagNESW; // ↗↙
    private Texture2D curDiagNWSE; // ↖↘

    void Awake()
    {
        winRect = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        parentRect = winRect.parent as RectTransform;
        BuildCursors();
        SetupCloseButton();
    }

    /// <summary>
    /// 把关闭按钮的锚点钉在其父节点的右上角(单锚点，不拉伸、不变形)。
    /// 之后窗口无论移动还是八方向缩放，Unity 布局系统都会让它实时跟随。
    /// </summary>
    private void SetupCloseButton()
    {
        if (closeButton == null) return;

        // 记录当前尺寸，避免改锚点后 sizeDelta 语义变化导致按钮变形
        Vector2 size = closeButton.rect.size;

        closeButton.anchorMin = new Vector2(1f, 1f);
        closeButton.anchorMax = new Vector2(1f, 1f);
        closeButton.pivot = new Vector2(1f, 1f);
        closeButton.sizeDelta = size;
        closeButton.anchoredPosition = closeButtonOffset;

        if (keepResizeCornerClickable)
        {
            // raycastPadding: (left, bottom, right, top)，正值向内收缩可点击区域。
            // 把按钮靠窗口最外侧的右边、上边各收进 borderThickness，
            // 视觉大小不变，但最外一条的点击会穿透给窗口用于右上角缩放。
            Graphic[] graphics = closeButton.GetComponentsInChildren<Graphic>(true);
            foreach (Graphic g in graphics)
            {
                Rect gr = g.rectTransform.rect;
                float padRight = Mathf.Min(borderThickness, Mathf.Max(0f, gr.width * 0.5f - 1f));
                float padTop = Mathf.Min(borderThickness, Mathf.Max(0f, gr.height * 0.5f - 1f));
                g.raycastPadding = new Vector4(0f, 0f, padRight, padTop);
            }
        }
    }

    void OnDestroy()
    {
        if (curHorizontal != null) Destroy(curHorizontal);
        if (curVertical != null) Destroy(curVertical);
        if (curDiagNESW != null) Destroy(curDiagNESW);
        if (curDiagNWSE != null) Destroy(curDiagNWSE);
    }

    private Camera GetEventCamera()
    {
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;
    }

    /// <summary>
    /// 判断鼠标落在窗口的哪个区域（与 pivot 无关）
    /// </summary>
    private DragRegion GetDragRegion(Vector2 localMouse)
    {
        // 注意：必须用 rect 而不是 sizeDelta。
        // 当锚点为拉伸模式(anchorMin != anchorMax)时，sizeDelta 不是真实尺寸，
        // 会导致整个窗口都被误判为边框区域，无法移动。
        Rect r = winRect.rect;
        float leftEdge = r.xMin;
        float rightEdge = r.xMax;
        float bottomEdge = r.yMin;
        float topEdge = r.yMax;

        float x = localMouse.x, y = localMouse.y;
        bool left = x < leftEdge + borderThickness;
        bool right = x > rightEdge - borderThickness;
        bool top = y > topEdge - borderThickness;
        bool bottom = y < bottomEdge + borderThickness;

        if (left && top) return DragRegion.LeftTop;
        if (right && top) return DragRegion.RightTop;
        if (left && bottom) return DragRegion.LeftBottom;
        if (right && bottom) return DragRegion.RightBottom;
        if (left) return DragRegion.Left;
        if (right) return DragRegion.Right;
        if (top) return DragRegion.Top;
        if (bottom) return DragRegion.Bottom;
        return DragRegion.Move;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            winRect, eventData.position, GetEventCamera(), out Vector2 localMouse);
        dragRegion = GetDragRegion(localMouse);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, GetEventCamera(), out startMouseLocal);

        if (dragRegion == DragRegion.Move)
        {
            moveOffset = startMouseLocal - winRect.anchoredPosition;
        }
        else
        {
            // 记录窗口四边在父节点中的坐标（与 anchor 无关）
            float pw = parentRect.rect.width;
            float ph = parentRect.rect.height;
            startLeft = winRect.anchorMin.x * pw + winRect.offsetMin.x;
            startRight = winRect.anchorMax.x * pw + winRect.offsetMax.x;
            startBottom = winRect.anchorMin.y * ph + winRect.offsetMin.y;
            startTop = winRect.anchorMax.y * ph + winRect.offsetMax.y;
        }

        if (enableCursor) SetCursor(dragRegion);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (dragRegion == DragRegion.Move)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect, eventData.position, GetEventCamera(), out Vector2 pos);
            winRect.anchoredPosition = pos - moveOffset;
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, GetEventCamera(), out Vector2 curMouse);
        Vector2 delta = curMouse - startMouseLocal;

        float left = startLeft, right = startRight;
        float top = startTop, bottom = startBottom;

        switch (dragRegion)
        {
            case DragRegion.Right:
                right = startRight + delta.x;
                break;
            case DragRegion.Left:
                left = startLeft + delta.x;
                break;
            case DragRegion.Top:
                top = startTop + delta.y;
                break;
            case DragRegion.Bottom:
                bottom = startBottom + delta.y;
                break;
            case DragRegion.RightBottom:
                right = startRight + delta.x;
                bottom = startBottom + delta.y;
                break;
            case DragRegion.RightTop:
                right = startRight + delta.x;
                top = startTop + delta.y;
                break;
            case DragRegion.LeftBottom:
                left = startLeft + delta.x;
                bottom = startBottom + delta.y;
                break;
            case DragRegion.LeftTop:
                left = startLeft + delta.x;
                top = startTop + delta.y;
                break;
        }

        // 约束尺寸，把被抓住的边锁在极值上，对边不动
        ClampEdges(ref left, ref right, ref top, ref bottom);

        // 通过 offsetMin/offsetMax 写回，自动适配任意 pivot/anchor
        ApplyEdges(left, right, top, bottom);
    }

    private void ClampEdges(ref float left, ref float right, ref float top, ref float bottom)
    {
        bool dragLeft = dragRegion == DragRegion.Left
                     || dragRegion == DragRegion.LeftTop
                     || dragRegion == DragRegion.LeftBottom;
        bool dragBottom = dragRegion == DragRegion.Bottom
                       || dragRegion == DragRegion.LeftBottom
                       || dragRegion == DragRegion.RightBottom;

        float width = right - left;
        if (width < minSize.x)
        {
            if (dragLeft) left = right - minSize.x;
            else right = left + minSize.x;
            width = minSize.x;
        }
        else if (width > maxSize.x)
        {
            if (dragLeft) left = right - maxSize.x;
            else right = left + maxSize.x;
            width = maxSize.x;
        }

        float height = top - bottom;
        if (height < minSize.y)
        {
            if (dragBottom) bottom = top - minSize.y;
            else top = bottom + minSize.y;
            height = minSize.y;
        }
        else if (height > maxSize.y)
        {
            if (dragBottom) bottom = top - maxSize.y;
            else top = bottom + maxSize.y;
            height = maxSize.y;
        }
    }

    private void ApplyEdges(float left, float right, float top, float bottom)
    {
        float pw = parentRect.rect.width;
        float ph = parentRect.rect.height;
        winRect.offsetMin = new Vector2(
            left - winRect.anchorMin.x * pw,
            bottom - winRect.anchorMin.y * ph);
        winRect.offsetMax = new Vector2(
            right - winRect.anchorMax.x * pw,
            top - winRect.anchorMax.y * ph);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        dragRegion = DragRegion.None;
        if (enableCursor) Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        if (!enableCursor) return;
        if (dragRegion != DragRegion.None) return; // 拖拽中保持拖拽时的光标

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            winRect, eventData.position, GetEventCamera(), out Vector2 localMouse);
        SetCursor(GetDragRegion(localMouse));
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!enableCursor) return;
        if (dragRegion == DragRegion.None)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void SetCursor(DragRegion region)
    {
        if (!enableCursor) return;
        Vector2 hotspot = new Vector2(8, 8);
        switch (region)
        {
            case DragRegion.Left:
            case DragRegion.Right:
                Cursor.SetCursor(curHorizontal, hotspot, CursorMode.Auto);
                break;
            case DragRegion.Top:
            case DragRegion.Bottom:
                Cursor.SetCursor(curVertical, hotspot, CursorMode.Auto);
                break;
            case DragRegion.LeftTop:
            case DragRegion.RightBottom:
                Cursor.SetCursor(curDiagNWSE, hotspot, CursorMode.Auto); // ↖↘
                break;
            case DragRegion.RightTop:
            case DragRegion.LeftBottom:
                Cursor.SetCursor(curDiagNESW, hotspot, CursorMode.Auto); // ↗↙
                break;
            default:
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                break;
        }
    }

    #region 运行时生成光标贴图

    private void BuildCursors()
    {
        curHorizontal = MakeLineCursor(cursorColor, true);
        curVertical = MakeLineCursor(cursorColor, false);
        curDiagNESW = MakeDiagCursor(cursorColor, false); // ↗↙
        curDiagNWSE = MakeDiagCursor(cursorColor, true);  // ↖↘
    }

    private Texture2D MakeLineCursor(Color color, bool horizontal)
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] px = new Color[size * size];
        int mid = size / 2;
        for (int i = 2; i < size - 2; i++)
        {
            if (horizontal)
            {
                px[mid * size + i] = color;
                px[(mid - 1) * size + i] = color * 0.6f;
            }
            else
            {
                px[i * size + mid] = color;
                px[i * size + (mid - 1)] = color * 0.6f;
            }
        }
        // 两端箭头
        for (int i = 0; i < 3; i++)
        {
            int off = 2 - i;
            if (horizontal)
            {
                px[(mid - i) * size + off] = color;
                px[(mid + i) * size + off] = color;
                px[(mid - i) * size + (size - 1 - off)] = color;
                px[(mid + i) * size + (size - 1 - off)] = color;
            }
            else
            {
                px[off * size + (mid - i)] = color;
                px[off * size + (mid + i)] = color;
                px[(size - 1 - off) * size + (mid - i)] = color;
                px[(size - 1 - off) * size + (mid + i)] = color;
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private Texture2D MakeDiagCursor(Color color, bool nwse)
    {
        const int size = 16;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;

        Color[] px = new Color[size * size];
        for (int y = 2; y < size - 2; y++)
        {
            int x = nwse ? y : (size - 1 - y);
            px[y * size + x] = color;
            if (x + 1 < size) px[y * size + x + 1] = color * 0.6f;
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    #endregion
}
