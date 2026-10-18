using System;
using System.Collections.Generic;
using System.Linq;

namespace JU.GameData
{
    /// <summary>公告与判定均读取同一份规则快照；语义分值不等同于底层数值或等级。</summary>
    public static class RuleEngine
    {
        public static List<DailyRule> Select(GameConfigData c, int day)
        {
            if (!c.enableDailyRules) return new List<DailyRule>();
            var matches = c.rules.Where(r => r.enabled && r.confirmed && day >= r.firstDay &&
                (r.lastDay == 0 || day <= r.lastDay) && (day - r.firstDay) % r.repeatEveryDays == 0)
                .OrderByDescending(r => r.priority).ThenBy(r => r.id, StringComparer.Ordinal).ToList();
            if (matches.Count < c.dailyRuleCount)
                throw new InvalidOperationException("第" + day + "天有效规则不足：需要" + c.dailyRuleCount + "条，找到" + matches.Count + "条。");
            return matches.Take(c.dailyRuleCount).Select(r => r.Copy()).ToList();
        }

        public static RuleEvaluation Evaluate(GameConfigData c, IEnumerable<DailyRule> rules, NpcAttributes a)
        {
            var result = new RuleEvaluation { success = true };
            foreach (var r in rules)
            {
                var band = c.bands.Single(b => a.Get(r.attribute) >= b.minValue && a.Get(r.attribute) <= b.maxValue);
                var semantic = c.semantics.Single(s => s.attribute == r.attribute && s.level == band.level);
                if (!semantic.confirmed) return new RuleEvaluation { error = r.id + "的语义映射尚未确认。" };
                float lowDistance = Math.Abs(semantic.score - r.lowAnchor), highDistance = Math.Abs(semantic.score - r.highAnchor);
                Decision target;
                if (semantic.score < r.lowAnchor || semantic.score > r.highAnchor)
                {
                    if (!r.unmatchedReject) return new RuleEvaluation { error = r.id + "语义分值超出规则范围。" };
                    target = Decision.Reject;
                }
                else if (lowDistance == highDistance)
                {
                    switch (r.tieBreak)
                    {
                        case TieBreakPolicy.Low: target = r.lowDecision; break;
                        case TieBreakPolicy.High: target = r.highDecision; break;
                        case TieBreakPolicy.Reject: target = Decision.Reject; break;
                        default: return new RuleEvaluation { error = r.id + "未配置等距处理。" };
                    }
                }
                else target = lowDistance < highDistance ? r.lowDecision : r.highDecision;
                result.traces.Add(r.id + " / " + r.attribute + " / 等级" + band.level + " / " + semantic.meaning +
                    " / 语义分值" + semantic.score + " / 结果" + ChoiceName(c, target));
                if (result.applicable && result.target != target && c.conflictPolicy == RuleConflictPolicy.RequireAgreement)
                    return new RuleEvaluation { error = "当天规则结论冲突：" + string.Join("；", result.traces), traces = result.traces };
                if (!result.applicable) result.target = target;
                result.applicable = true;
                // HighestPriority 仍计算并记录所有规则；最高优先级（同级按ID排序）的结果用于最终判定。
            }
            return result;
        }

        public static string WorldName(GameConfigData c, Destination id)
        { return c.worlds.Single(w => w.id == id).displayName; }
        public static string ChoiceName(GameConfigData c, Decision d)
        {
            if (d == Decision.Reject) return "拒签";
            if (d == Decision.Detain) return "扣留（本版关闭）";
            if (d != Decision.ReleaseToUtopia && d != Decision.ReleaseToAntiUtopia) throw new ArgumentOutOfRangeException("d");
            return "放行" + WorldName(c, d == Decision.ReleaseToUtopia ? Destination.Utopia : Destination.AntiUtopia);
        }
        public static string Announcement(GameConfigData c, DailyRule r)
        {
            var lines = c.semantics.Where(s => s.attribute == r.attribute).OrderBy(s => s.level).Select(s =>
            {
                var band = c.bands.Single(b => b.level == s.level);
                var evaluation = Evaluate(c, new[] { r }, new NpcAttributes { memory = band.minValue });
                return s.meaning + " → " + (evaluation.success ? ChoiceName(c, evaluation.target) : evaluation.error);
            });
            return r.title + "（" + r.id + "）\n" + string.Join("\n", lines) +
                "\n申报目的地符合上述准入目标时放行，否则拒签。来源和申报不代替记忆规则。";
        }
    }
}
