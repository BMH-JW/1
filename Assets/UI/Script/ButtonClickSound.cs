using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 给单个 Button 挂的点击音效组件 —— 拖一个 AudioClip 进去就响。
/// 每个按钮可以配完全不同的音效，互不影响。
/// 不依赖 AutoButtonSound，可以共存。
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonClickSound : MonoBehaviour
{
    [Tooltip("这个按钮专属的点击音效 —— 从 AudioManager.sfxSource 播，自动受 SFX 音量滑块控制")]
    public AudioClip clickSfx;

    [Tooltip("音量缩放（0~1，只影响这一个音效）")]
    [Range(0f, 1f)] public float volumeScale = 1f;

    Button _btn;

    void Awake()
    {
        _btn = GetComponent<Button>();
    }

    void OnEnable()
    {
        if (_btn != null) _btn.onClick.AddListener(Play);
    }

    void OnDisable()
    {
        if (_btn != null) _btn.onClick.RemoveListener(Play);
    }

    void Play()
    {
        if (clickSfx == null || AudioManager.Instance == null) return;
        AudioManager.Instance.PlaySFX(clickSfx, volumeScale);
    }
}
