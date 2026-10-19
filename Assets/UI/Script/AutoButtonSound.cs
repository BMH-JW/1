using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 批量给所有 Button 加"同一个"默认点击音效 —— 挂在 Canvas 上。
/// 适合做原型阶段、还没给每个按钮配专属音效时快速占位。
/// 注意：ButtonClickSound（单独挂的）优先级更高，AutoButtonSound 会跳过已经挂了 ButtonClickSound 的按钮，
/// 避免两个音效同时响。RoomInteract / NPCBellLogic 自带音效的也会跳过。
/// 🔲 如果不需要批量默认音效，直接不挂这个组件就行。
/// </summary>
public class AutoButtonSound : MonoBehaviour
{
    void Start()
    {
        StartCoroutine(BindAllButtonsNextFrame());
    }

    System.Collections.IEnumerator BindAllButtonsNextFrame()
    {
        yield return null;
        BindAllButtons();
    }

    private void BindAllButtons()
    {
        var allButtons = FindObjectsOfType<Button>(true);
        int count = 0;
        foreach (var btn in allButtons)
        {
            // 跳过已经有专属音效组件的
            if (btn.GetComponent<ButtonClickSound>() != null) continue;
            // 跳过自带音效的
            if (btn.GetComponent<NPCBellLogic>() != null) continue;
            if (btn.GetComponent<RoomInteract>() != null) continue;

            btn.onClick.AddListener(PlayDefaultClick);
            count++;
        }
        Debug.Log($"[AutoButtonSound] 已绑定 {count} 个 Button 默认点击音效（跳过 {allButtons.Length - count} 个已有专属音效）");
    }

    private void PlayDefaultClick()
    {
        if (AudioManager.Instance != null && AudioManager.Instance.defaultClickSfx != null)
        {
            AudioManager.Instance.PlaySFX(AudioManager.Instance.defaultClickSfx);
        }
    }
}
