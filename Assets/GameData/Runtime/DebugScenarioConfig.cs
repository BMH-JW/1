namespace JU.GameData
{
    /// <summary>仅开启当前测试配置的开发日志，不再注入旧版配额、调查或世界权重假设。</summary>
    public static class DebugScenarioConfig
    {
        public static GameConfigData Create(GameConfigData source)
        { var c = source.Copy(); c.testConfiguration = true; c.debugMode = c.logRuleResult = true; return c; }
    }
}
