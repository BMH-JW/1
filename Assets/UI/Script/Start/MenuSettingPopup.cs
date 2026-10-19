using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 开始界面设置面板 —— 开关面板 + 三个音量滑块（Master / BGM / SFX）。
/// 滑块绑定 AudioManager.SetMaster/SetBGM/SetSFX，值自动 PlayerPrefs 持久化。
/// </summary>
public class MenuSettingPopup : MonoBehaviour
{
    [Header("面板开关")]
    public GameObject panelSetting;
    public Button btnSetting;
    public Button btnCloseSetting;

    [Header("音量滑块（0~1）")]
    public Slider masterSlider;   // 主音量
    public Slider bgmSlider;      // BGM 音量
    public Slider sfxSlider;      // 音效音量

    void Start()
    {
        // 面板开关
        if (btnSetting != null) btnSetting.onClick.AddListener(OpenSettingPanel);
        if (btnCloseSetting != null) btnCloseSetting.onClick.AddListener(CloseSettingPanel);

        // 音量滑块初始化 —— 从 AudioManager（或 PlayerPrefs）读当前值
        InitSlider(masterSlider, AudioManager.Instance?.Master ?? 1f, AudioManager.Instance != null ? (System.Action<float>)AudioManager.Instance.SetMaster : null);
        InitSlider(bgmSlider,    AudioManager.Instance?.BGM    ?? 0.8f, AudioManager.Instance != null ? (System.Action<float>)AudioManager.Instance.SetBGM    : null);
        InitSlider(sfxSlider,    AudioManager.Instance?.SFX    ?? 1f,   AudioManager.Instance != null ? (System.Action<float>)AudioManager.Instance.SetSFX    : null);
    }

    /// <summary>统一初始化一个滑块：设 value + 注册 onValueChanged。</summary>
    private void InitSlider(Slider slider, float initial, System.Action<float> setter)
    {
        if (slider == null) return;

        slider.wholeNumbers = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        // 先移除所有旧的监听（防止重复订阅）
        slider.onValueChanged.RemoveAllListeners();

        // 设初始值 —— 用 setter 的初始输入（AudioManager 会用当前 master×bgm，但这里给滑块显示 master 部分）
        // 注意：AudioManager.SetBGM 接受的是 0~1 原始 bgmVolume（不是乘 master 后的值）
        // 所以滑块应该绑定到原始值，从 PlayerPrefs 读
        if (AudioManager.Instance == null)
        {
            // AudioManager 可能还没初始化（StartScene 顺序问题），从 PlayerPrefs 兜底
            const string K_MASTER = "Vol_Master";
            const string K_BGM   = "Vol_BGM";
            const string K_SFX   = "Vol_SFX";
            if (slider == masterSlider) slider.value = PlayerPrefs.GetFloat(K_MASTER, initial);
            else if (slider == bgmSlider) slider.value = PlayerPrefs.GetFloat(K_BGM, 0.8f);
            else if (slider == sfxSlider) slider.value = PlayerPrefs.GetFloat(K_SFX, 1f);
        }
        else
        {
            // 直接用 AudioManager 的原始 volume（不是乘 master 后的 BGM/SFX）
            if (slider == masterSlider) slider.value = AudioManager.Instance.masterVolume;
            else if (slider == bgmSlider) slider.value = AudioManager.Instance.bgmVolume;
            else if (slider == sfxSlider) slider.value = AudioManager.Instance.sfxVolume;
        }

        // 只注册 setter —— 初始值设置不会触发 onValueChanged（因为我们用 slider.value = ... 之后才 AddListener）
        if (setter != null) slider.onValueChanged.AddListener(v => setter(v));
    }

    public void OpenSettingPanel()
    {
        // 每次打开面板都刷新一遍滑块（防止 AudioManager 后来才初始化）
        RefreshSliders();
        if (panelSetting != null) panelSetting.SetActive(true);
    }

    public void CloseSettingPanel()
    {
        if (panelSetting != null) panelSetting.SetActive(false);
    }

    private void RefreshSliders()
    {
        if (AudioManager.Instance == null) return;
        if (masterSlider != null) masterSlider.value = AudioManager.Instance.masterVolume;
        if (bgmSlider    != null) bgmSlider.value    = AudioManager.Instance.bgmVolume;
        if (sfxSlider    != null) sfxSlider.value    = AudioManager.Instance.sfxVolume;
    }

    void OnDestroy()
    {
        if (btnSetting != null) btnSetting.onClick.RemoveListener(OpenSettingPanel);
        if (btnCloseSetting != null) btnCloseSetting.onClick.RemoveListener(CloseSettingPanel);
        if (masterSlider != null) masterSlider.onValueChanged.RemoveAllListeners();
        if (bgmSlider    != null) bgmSlider.onValueChanged.RemoveAllListeners();
        if (sfxSlider    != null) sfxSlider.onValueChanged.RemoveAllListeners();
    }
}
