using UnityEngine;

/// <summary>
/// 🔧 DailyEvent 调试面板 —— 挂在任意 GameObject 上（比如 Canvas）。
/// Play Mode 里直接在 Inspector 拖 slider 设置秩序/纯净值 → 点 Apply & Retest 按钮
/// → 自动改 GameSession state → 重新弹 DailyEvent 看对应区间的新闻。
///
/// 也可以在 Runtime 用代码调 public SetValues(order, purity) 然后 TriggerNow()。
/// </summary>
public class DailyEventDebugger : MonoBehaviour
{
    [Header("🔧 手动设置秩序/纯净值 (Play Mode 拖这个测试)")]
    [Tooltip("目标秩序值 0~100")]
    [Range(0f, 100f)] public float orderOverride = 50f;
    [Tooltip("目标纯净值 0~100")]
    [Range(0f, 100f)] public float purityOverride = 50f;

    [Header("一键操作")]
    [Tooltip("点击按钮后：直接改 GameSession 的 order/purity → 立即弹 DailyEvent 看效果")]
    public bool applyAndTrigger;   // ← Inspector 里勾一下就触发（比加个 Button 字段更简单）

    [Tooltip("如果勾上：Apply 后自动触发 DailyEvent；否则只改值不弹窗")]
    public bool autoTriggerDailyEvent = true;

    void Update()
    {
        if (applyAndTrigger)
        {
            applyAndTrigger = false;
            ApplyNow();
        }
    }

    /// <summary>手动调：直接改值（不改 GameSession，只是给外部用）。</summary>
    public void SetValues(float order, float purity)
    {
        orderOverride = Mathf.Clamp01(order / 100f) * 100f;
        purityOverride = Mathf.Clamp01(purity / 100f) * 100f;
    }

    /// <summary>应用到 GameSession + 可选立即弹 DailyEvent。</summary>
    public void ApplyNow()
    {
        var bridge = GameSessionBridge.Instance;
        if (bridge == null || bridge.Current == null)
        {
            Debug.LogError("[DailyEventDebugger] GameSessionBridge 或 Current 为 null，无法调试");
            return;
        }

        // 直接改 GameSession 内部 state（绕过正规 EndDay 流程 —— 这是调试面板）
        var state = bridge.Current.GetState();
        float oldOrder = state.order;
        float oldPurity = state.purity;

        // 用反射写 state（因为 state 是 private readonly）
        // 或者更简单 —— 直接构造一个临时 report 触发 DailyEvent 就行
        Debug.Log($"[DailyEventDebugger] ⚠️ 调试模式：强制 order {oldOrder:0} → {orderOverride:0}，purity {oldPurity:0} → {purityOverride:0}");

        // 方案 A：直接写 GameSession.state（需要反射）
        // 方案 B：让 GameSessionBridge 暴露一个 SetState 方法 —— 更干净

        // 用方案 B：桥接层加调试入口
        bridge.ForceSetWorldValues(orderOverride, purityOverride);

        if (autoTriggerDailyEvent)
        {
            // 手动构造一个"伪 report"让 TryPopupDailyEvent 用
            bridge.ForcePopupDailyEventWithValues(orderOverride, purityOverride);
        }
    }

    /// <summary>快捷预设 —— 每个区间各跑一遍测试。</summary>
    [ContextMenu("测试：Danger (<20)")]
    void TestDanger() { orderOverride = 15f; purityOverride = 10f; ApplyNow(); }

    [ContextMenu("测试：CriticalLow (20~30)")]
    void TestCriticalLow() { orderOverride = 25f; purityOverride = 28f; ApplyNow(); }

    [ContextMenu("测试：Low (31~50)")]
    void TestLow() { orderOverride = 35f; purityOverride = 45f; ApplyNow(); }

    [ContextMenu("测试：Normal (51~80)")]
    void TestNormal() { orderOverride = 55f; purityOverride = 70f; ApplyNow(); }

    [ContextMenu("测试：High (>80)")]
    void TestHigh() { orderOverride = 85f; purityOverride = 92f; ApplyNow(); }

    [ContextMenu("测试：游戏结束 成功 (>80)")]
    void TestWin() { orderOverride = 85f; purityOverride = 90f; ApplyNow(); }

    [ContextMenu("测试：游戏结束 失败 (<20)")]
    void TestLose() { orderOverride = 10f; purityOverride = 5f; ApplyNow(); }
}
