using UnityEngine;

/// <summary>
/// 全局音量管理器（单例，DontDestroyOnLoad 跨场景持久化）。
/// 三条音量线：Master / BGM / SFX —— 分别对应 AudioListener.volume 和两个 AudioSource。
/// 音量值自动 PlayerPrefs 持久化，下次打开游戏恢复。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("AudioSource 引用（可留空，Awake 自动创建）")]
    [Tooltip("背景音乐 AudioSource —— loop=true，priority=0；不拖也没事，Awake 自动建子物体")]
    public AudioSource bgmSource;
    [Tooltip("音效 AudioSource —— loop=false，按需 PlayOneShot")]
    public AudioSource sfxSource;

    [Header("🎵 音频资源（直接拖你的 .mp3/.wav 文件进来）")]
    [Tooltip("背景音乐 —— 游戏启动自动循环播放")]
    public AudioClip bgmClip;
    [Tooltip("启动时是否自动播放 BGM")]
    public bool autoPlayBGM = true;

    [Tooltip("全局默认 UI 点击音效 —— 按钮没单独配 clickSfx 时用这个")]
    public AudioClip defaultClickSfx;

    [Header("音量（0~1，Inspector 可直接调默认值）")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float bgmVolume = 0.8f;
    [Range(0f, 1f)] public float sfxVolume = 1f;

    // 组合音量 —— 实际播放音量 = master × bgm / master × sfx
    public float Master => masterVolume;
    public float BGM => bgmVolume * masterVolume;
    public float SFX => sfxVolume * masterVolume;

    // PlayerPrefs key
    const string K_MASTER = "Vol_Master";
    const string K_BGM = "Vol_BGM";
    const string K_SFX   = "Vol_SFX";

    void Awake()
    {
        // 单例 + 跨场景持久化
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 从 PlayerPrefs 加载已保存的音量（没保存过就用 Inspector 默认值）
        if (PlayerPrefs.HasKey(K_MASTER)) masterVolume = PlayerPrefs.GetFloat(K_MASTER);
        if (PlayerPrefs.HasKey(K_BGM))   bgmVolume   = PlayerPrefs.GetFloat(K_BGM);
        if (PlayerPrefs.HasKey(K_SFX))   sfxVolume   = PlayerPrefs.GetFloat(K_SFX);

        // 自动创建 AudioSource（如果 Inspector 没拖）
        EnsureSources();

        // 首次应用音量
        ApplyAll();

        // 自动播放 BGM（如果 Inspector 拖了 bgmClip 且 autoPlayBGM=true）
        if (autoPlayBGM && bgmClip != null)
        {
            PlayBGM(bgmClip);
            Debug.Log($"[AudioManager] 自动播放 BGM: {bgmClip.name}");
        }

        Debug.Log($"[AudioManager] 初始化 ✅ master={masterVolume:F2} bgm={bgmVolume:F2} sfx={sfxVolume:F2}");
    }

    /// <summary>保证 bgmSource / sfxSource 都有（没拖就自动在自身创建子物体）。</summary>
    private void EnsureSources()
    {
        if (bgmSource == null)
        {
            GameObject bgmGO = new GameObject("BGM_Source");
            bgmGO.transform.SetParent(transform, false);
            bgmSource = bgmGO.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.priority = 0;   // 0 = 最高优先级，保证不被音效抢占
            bgmSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            GameObject sfxGO = new GameObject("SFX_Source");
            sfxGO.transform.SetParent(transform, false);
            sfxSource = sfxGO.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.priority = 128; // 默认优先级
            sfxSource.playOnAwake = false;
        }
    }

    // =============================================================
    // 公共 API —— 供 UI 滑块 / 其他脚本调用
    // =============================================================

    public void SetMaster(float v)
    {
        masterVolume = Mathf.Clamp01(v);
        ApplyAll();
        SaveAll();
    }

    public void SetBGM(float v)
    {
        bgmVolume = Mathf.Clamp01(v);
        ApplyAll();
        SaveAll();
    }

    public void SetSFX(float v)
    {
        sfxVolume = Mathf.Clamp01(v);
        ApplyAll();
        SaveAll();
    }

    // 便捷方法 —— 外部播 BGM / SFX 不用关心音量乘算
    public void PlayBGM(AudioClip clip, bool forceRestart = false)
    {
        if (clip == null || bgmSource == null) return;
        if (!forceRestart && bgmSource.clip == clip && bgmSource.isPlaying) return;
        bgmSource.clip = clip;
        bgmSource.volume = BGM;
        bgmSource.Play();
    }

    public void StopBGM()
    {
        if (bgmSource != null) bgmSource.Stop();
    }

    public void PlaySFX(AudioClip clip, float volumeScale = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, SFX * Mathf.Clamp01(volumeScale));
    }

    // =============================================================
    // 内部：应用 + 持久化
    // =============================================================

    private void ApplyAll()
    {
        if (AudioListener.volume != masterVolume) AudioListener.volume = masterVolume;
        if (bgmSource != null) bgmSource.volume = BGM;
        if (sfxSource != null) sfxSource.volume = SFX;
    }

    private void SaveAll()
    {
        PlayerPrefs.SetFloat(K_MASTER, masterVolume);
        PlayerPrefs.SetFloat(K_BGM,   bgmVolume);
        PlayerPrefs.SetFloat(K_SFX,   sfxVolume);
        PlayerPrefs.Save();
    }
}
