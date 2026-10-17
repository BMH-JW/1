using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ESC 暂停管理器（自包含，挂在场景任意物体上）。
/// 按 ESC：整个游戏画面变暗 + 中央显示暂停页面图片（图片不变暗）+ 时间暂停；
/// 再按 ESC：恢复原样。
/// </summary>
public class PauseManager : MonoBehaviour
{
    [Header("暂停页面图片")]
    [Tooltip("暂停时中央显示的图片（Sprite），不受变暗影响")]
    public Sprite pausePageSprite;

    [Header("外观（可改）")]
    [Tooltip("暂停图片的大小（像素，基于1920x1080参考分辨率）")]
    public Vector2 pausePageSize = new Vector2(800f, 600f);
    [Tooltip("画面变暗的程度 0~1")]
    [Range(0f, 1f)] public float dimAlpha = 0.7f;
    [Tooltip("是否暂停游戏时间（Time.timeScale=0）")]
    public bool pauseTimeScale = true;

    private GameObject _pauseRoot;
    private bool _paused;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    public void TogglePause()
    {
        if (_paused) Resume();
        else Pause();
    }

    private void Pause()
    {
        _paused = true;
        if (pauseTimeScale) Time.timeScale = 0f;
        BuildPauseUI();
        Debug.Log("[PauseManager] 游戏暂停");
    }

    private void Resume()
    {
        _paused = false;
        if (pauseTimeScale) Time.timeScale = 1f;
        if (_pauseRoot != null)
        {
            Destroy(_pauseRoot);
            _pauseRoot = null;
        }
        Debug.Log("[PauseManager] 游戏恢复");
    }

    private void BuildPauseUI()
    {
        if (_pauseRoot != null) Destroy(_pauseRoot);

        // 独立 Canvas，ScreenSpaceOverlay + 最高排序，保证压在所有游戏UI之上
        _pauseRoot = new GameObject("PauseRoot");
        Canvas canvas = _pauseRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        _pauseRoot.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = _pauseRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        // 1. 变暗遮罩（全屏黑色半透明，先创建 = 渲染在底层）
        GameObject dimGo = new GameObject("Dim");
        dimGo.transform.SetParent(_pauseRoot.transform, false);
        Image dimImg = dimGo.AddComponent<Image>();
        dimImg.color = new Color(0f, 0f, 0f, dimAlpha);
        RectTransform dimRT = dimImg.rectTransform;
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        // 2. 暂停页面图片（后创建 = 渲染在遮罩之上，不受变暗影响）
        GameObject pageGo = new GameObject("PausePage");
        pageGo.transform.SetParent(_pauseRoot.transform, false);
        Image pageImg = pageGo.AddComponent<Image>();
        pageImg.sprite = pausePageSprite;
        pageImg.preserveAspect = true;
        pageImg.raycastTarget = false; // 图片本身不挡点击，点击由遮罩挡住
        RectTransform pageRT = pageImg.rectTransform;
        pageRT.anchorMin = new Vector2(0.5f, 0.5f);
        pageRT.anchorMax = new Vector2(0.5f, 0.5f);
        pageRT.pivot = new Vector2(0.5f, 0.5f);
        pageRT.anchoredPosition = Vector2.zero;
        pageRT.sizeDelta = pausePageSize;

        if (pausePageSprite == null)
            Debug.LogWarning("[PauseManager] pausePageSprite 未设置，暂停页将是空白");
    }
}
