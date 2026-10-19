using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace JU.GameData
{
    public static class V3Validation
    {
        public static readonly string[] Axes = { "memory", "relationship", "desire", "will" };
        private static readonly string[] DocTokens = { "name", "origin", "motive", "motive_desc", "destination", "utopia", "anti_utopia" };
        private static readonly string[] FeedbackTokens = { "day", "name", "choice", "source", "destination", "purityDelta", "orderDelta", "actualPurityDelta", "actualOrderDelta", "utopia", "anti_utopia" };
        public static bool Finite(float x) { return !float.IsNaN(x) && !float.IsInfinity(x); }
        public static List<string> Validate(GameConfigData c)
        {
            var e = new List<string>();
            if (c == null) { e.Add("配置为空。"); return e; }
            var lists = new IEnumerable<ConfigRow>[] { c.worlds, c.bands, c.generationWeights, c.semantics, c.rules, c.formulas,
                c.tags, c.details, c.motives, c.documents, c.items, c.identities, c.portraitPools, c.feedbackTemplates };
            if (lists.Any(x => x == null) || c.pendingDecisions == null || c.implementationDecisions == null || c.lightFeedbackThresholds == null || c.formalEventThresholds == null)
            { e.Add("配置列表不能为null。"); return e; }
            if (lists.Any(xs => xs.Any(x => x == null))) { e.Add("配置条目不能为null。"); return e; }
            if (c.startDay < 1 || c.startDay > 1000000 || c.minDailyApplicants < 1 || c.maxDailyApplicants < c.minDailyApplicants || c.maxDailyApplicants > 1000) e.Add("起始天数1～1000000；每日人数下限≥1，上限≥下限且≤1000。");
            if (c.worldValueMin != 0 || c.worldValueMax != 100 || c.trustMin != 0 || c.trustMax != 100) e.Add("两界状态和信任范围统一为0～100。");
            if (!Finite(c.initialPurity) || !Finite(c.initialOrder) || c.initialPurity < 0 || c.initialPurity > 100 || c.initialOrder < 0 || c.initialOrder > 100 ||
                c.initialPurity != (float)Math.Round(c.initialPurity) || c.initialOrder != (float)Math.Round(c.initialOrder)) e.Add("世界初始值须为0～100的整数。");
            if (c.initialTrust < 0 || c.initialTrust > 100) e.Add("信任初始值须为0～100。");
            if (c.complianceReward < 0 || c.complianceReward > 100000 || c.violationPenalty < 0 || c.violationPenalty > 100000) e.Add("信任奖惩幅度须为0～100000。");
            if (c.impactDivisor < 1 || c.impactDivisor > 1000 || c.rejectionSourcePenalty < 0 || c.rejectionSourcePenalty > 100) e.Add("世界影响除数须为1～1000，拒签来源扣除须为0～100。");
            if (c.dailyRuleCount < 1 || c.feedbackDelayDays < 1 || c.feedbackDelayDays > 365) e.Add("每日规则数≥1，反馈延迟1～365天。");
            if (string.IsNullOrWhiteSpace(c.bondTraceLabel) || string.IsNullOrWhiteSpace(c.driveAssessmentLabel)) e.Add("两类档案的显示名称不能为空。");
            // 世界配置：必须两个，id 映射正确；displayName 是纯显示用的，不阻断运行
            if (c.worlds.Count != 2) e.Add("世界配置须恰好为两个（Utopia + AntiUtopia）。");
            else
            {
                int utopiaCount = c.worlds.Count(w => w.id == Destination.Utopia);
                int antiCount = c.worlds.Count(w => w.id == Destination.AntiUtopia);
                if (utopiaCount != 1 || antiCount != 1) e.Add("世界配置须各有一个 Utopia(id=0) 和 AntiUtopia(id=1)。");
            }
            if (c.bands.Count != 5 || c.bands.Select(b => b.level).Distinct().Count() != 5 || c.bands.Any(b => b.level < 1 || b.level > 5 || b.minValue < -10 || b.maxValue > 10 || b.minValue > b.maxValue)) e.Add("表1须为唯一1～5级有效区间。");
            for (int v = -10; v <= 10; v++) if (c.bands.Count(b => v >= b.minValue && v <= b.maxValue) != 1) e.Add("数值" + v + "的等级映射缺失或重叠。");
            if (c.generationWeights.Count != 5 || c.generationWeights.Select(w => w.level).Distinct().Count() != 5 || c.generationWeights.Any(w => w.level < 1 || w.level > 5 || !Finite(w.weight) || w.weight < 0) || Math.Abs(c.generationWeights.Sum(w => (double)w.weight) - 1) > 0.0001) e.Add("表6须完整覆盖1～5级，权重非负且和为1。");
            foreach (string axis in Axes) for (int level = 1; level <= 5; level++) if (c.semantics.Count(s => s.attribute == axis && s.level == level) != 1) e.Add("语义缺失或重复：" + axis + "/" + level);
            if (c.semantics.Count != 20 || c.semantics.Any(s => !Finite(s.score) || string.IsNullOrWhiteSpace(s.meaning))) e.Add("需20条有效属性语义。");
            foreach (var pair in new[] { new[] { "memory", "relationship" }, new[] { "desire", "will" } })
                for (int x = 1; x <= 5; x++) for (int y = 1; y <= 5; y++) if (c.tags.Count(t => t.axisX == pair[0] && t.axisY == pair[1] && t.xLevel == x && t.yLevel == y) != 1) e.Add("二维描述缺失或重复：" + pair[0] + "/" + x + "/" + y);
            if (c.tags.Count != 50 || c.tags.Any(t => string.IsNullOrWhiteSpace(t.description))) e.Add("需50条非空二维描述。");
            foreach (string axis in Axes.Concat(new[] { "desire_body" })) for (int l = 1; l <= 5; l++) if (c.details.Count(d => d.attribute == axis && d.level == l) != 1) e.Add("一维明细缺失或重复：" + axis + "/" + l);
            if (c.details.Count != 25 || c.details.Any(d => string.IsNullOrWhiteSpace(d.text))) e.Add("需25条非空一维明细。");
            Unique(c.tags.Select(x => x.id.ToString()), "二维描述", e); Unique(c.details.Select(x => x.id.ToString()), "一维明细", e);
            Unique(c.motives.Select(x => x.id.ToString()), "动机", e); Unique(c.motives.Select(x => x.name), "动机名", e);
            Unique(c.documents.Select(x => x.id.ToString()), "文书", e); Unique(c.items.Select(x => x.id), "道具", e);
            Unique(c.identities.Select(x => x.id), "人物身份", e); Unique(c.identities.Select(x => x.name), "人物姓名", e);
            Unique(c.portraitPools.Select(x => x.origin), "原籍立绘池", e); Unique(c.rules.Select(x => x.id), "规则", e);
            Unique(c.formulas.Select(x => x.id), "公式", e); Unique(c.feedbackTemplates.Select(x => x.id), "反馈模板", e);
            if (c.motives.Any(m => m.id <= 0 || string.IsNullOrWhiteSpace(m.description))) e.Add("动机编号须为正且描述非空。");
            foreach (var pool in c.portraitPools)
                if (pool.portraitKeys == null || pool.portraitKeys.Count == 0 || pool.portraitKeys.Any(string.IsNullOrWhiteSpace) || pool.portraitKeys.Distinct().Count() != pool.portraitKeys.Count) e.Add(pool.origin + "必须配置非空且无重复的立绘Key。");
            foreach (var person in c.identities)
            {
                var pool = c.portraitPools.FirstOrDefault(o => o.origin == person.origin);
                if (pool == null || (pool.portraitKeys != null && !string.IsNullOrEmpty(person.portraitKey) && !pool.portraitKeys.Contains(person.portraitKey))) e.Add(person.id + "原籍或指定立绘不在对应池中。");
                if (!Enum.IsDefined(typeof(Deception), person.declarationType) || !c.motives.Any(m => m.id == person.motiveId)) e.Add(person.id + "须显式指定有效申报类型和动机，不能回退到F003。");
                if (person.fixedAttributes == null || (person.useFixedAttributes && Axes.Any(a => person.fixedAttributes.Get(a) < -10 || person.fixedAttributes.Get(a) > 10))) e.Add(person.id + "固定属性必须在-10～10。");
            }
            if (c.identities.Count < c.maxDailyApplicants) e.Add("人物数量必须≥每日人数上限，不能通过同日重名补足。");
            Unique(c.documents.Select(d => d.field + "/" + d.motive + "/" + d.destination + "/" + d.triggerType), "文书匹配键", e);
            foreach (var d in c.documents)
            {
                if ((d.field != "body_state" && d.field != "entry_reason") || (d.destination != "any" && d.destination != "utopia" && d.destination != "anti_utopia") ||
                    (d.motive != "any" && !c.motives.Any(m => m.name == d.motive)) || d.triggerType < 0 || d.triggerType > 2 || string.IsNullOrWhiteSpace(d.text)) e.Add("文书" + d.id + "匹配条件或正文无效。");
                CheckTokens(d.text, DocTokens, "文书" + d.id, e);
            }
            foreach (var r in c.rules)
            {
                if (r.firstDay < 1 || r.lastDay < 0 || (r.lastDay > 0 && r.lastDay < r.firstDay) || r.repeatEveryDays < 1) e.Add(r.id + "日期安排无效。");
                if (r.enabled && (!r.confirmed || r.attribute != "memory" || string.IsNullOrWhiteSpace(r.title) || !Finite(r.lowAnchor) || !Finite(r.highAnchor) || r.lowAnchor >= r.highAnchor ||
                    r.tieBreak == TieBreakPolicy.Unconfigured || !Enum.IsDefined(typeof(TieBreakPolicy), r.tieBreak) || !IsRoutingDecision(r.lowDecision) || !IsRoutingDecision(r.highDecision))) e.Add(r.id + "本版只允许已确认的记忆准入规则及有效去向、中点策略。");
                if (r.enabled && c.semantics.Any(s => s.attribute == r.attribute && !s.confirmed)) e.Add(r.id + "引用未确认语义。");
            }
            if (!Enum.IsDefined(typeof(RuleConflictPolicy), c.conflictPolicy)) e.Add("规则冲突策略无效。");
            for (int i = 1; i <= 15; i++) if (!c.formulas.Any(f => f.id == "F" + i.ToString("D3"))) e.Add("公式库缺少F" + i.ToString("D3"));
            if (c.formulas.Count != 15) e.Add("公式库须完整保留F001～F015。");
            foreach (var f in c.formulas.Where(f => f.enabled))
            {
                if (!f.confirmed) e.Add(f.id + "未确认，不能启用。");
                if (!new[] { "F001", "F002", "F004", "F005", "F013" }.Contains(f.id)) e.Add(f.id + "不属于本版运行范围，不能启用。");
                if (f.testOnly && !c.testConfiguration) e.Add(f.id + "是测试公式，不能当作最终配置。");
            }
            if (c.enablePopulationFeedback || c.enableThresholdEvents) e.Add("生成权重反馈和阈值事件尚缺完整规则，本版不能开启。");
            if (c.lightFeedbackThresholds.Concat(c.formalEventThresholds).Any(v => !Finite(v) || v < 0 || v > 100)) e.Add("保留的阈值必须在0～100。");
            foreach (var f in c.feedbackTemplates)
            {
                if (!IsRoutingDecision(f.decision) || f.compliance < -1 || f.compliance > 1 || string.IsNullOrWhiteSpace(f.text)) e.Add(f.id + "反馈条件或正文无效。");
                CheckTokens(f.text, FeedbackTokens, "反馈" + f.id, e);
            }
            Unique(c.feedbackTemplates.Select(f => f.decision + "/" + f.compliance), "反馈匹配键", e);
            return e.Distinct().ToList();
        }
        public static List<string> ForPlay(GameConfigData c, bool release)
        {
            var e = Validate(c); if (e.Count > 0) return e;
            foreach (string id in new[] { "F001", "F002" }) if (!c.formulas.Any(f => f.id == id && f.enabled)) e.Add(id + "是本版生成链依赖，须启用。");
            try { RuleEngine.Select(c, c.startDay); } catch (Exception ex) { e.Add(ex.Message); }
            if (c.enableDocuments) foreach (var person in c.identities) foreach (string dest in new[] { "utopia", "anti_utopia" })
            {
                string motive = c.motives.Single(m => m.id == person.motiveId).name;
                foreach (string field in new[] { "entry_reason", "body_state" })
                    if (!(field == "body_state" && person.declarationType == Deception.Honest) && !c.documents.Any(d => d.field == field && d.triggerType == (int)person.declarationType && (d.destination == "any" || d.destination == dest) && (d.motive == "any" || d.motive == motive))) e.Add("文书缺少覆盖：" + person.id + "/" + field + "/" + dest + "/" + person.declarationType);
            }
            if (c.enableDelayedFeedback) foreach (Decision d in new[] { Decision.ReleaseToUtopia, Decision.ReleaseToAntiUtopia, Decision.Reject })
                if (!c.feedbackTemplates.Any(t => t.decision == d && t.compliance == -1)) e.Add("反馈缺少通用处理模板：" + d);
            if (release || !c.testConfiguration)
            {
                if (c.testConfiguration) e.Add("当前为测试配置，不能标记为正式发布配置。");
                if (c.identities.Any(p => p.sampleOnly) || c.portraitPools.Any(p => p.portraitKeys.Any(k => k.StartsWith("sample:", StringComparison.Ordinal)))) e.Add("样本人物或占位立绘尚未替换。");
                if (c.feedbackTemplates.Any(t => t.testOnly) && c.enableDelayedFeedback) e.Add("留声机仍有调试事实模板，不能当作正式叙事。");
                if (c.formulas.Any(f => f.enabled && f.testOnly)) e.Add("世界或信任测试公式尚未完成正式平衡确认。");
            }
            return e.Distinct().ToList();
        }
        private static bool IsRoutingDecision(Decision d) { return d == Decision.ReleaseToUtopia || d == Decision.ReleaseToAntiUtopia || d == Decision.Reject; }
        private static void Unique(IEnumerable<string> ids, string label, List<string> e)
        { var xs = ids.ToList(); if (xs.Any(string.IsNullOrWhiteSpace) || xs.Distinct().Count() != xs.Count) e.Add(label + "为空或重复。"); }
        private static void CheckTokens(string text, string[] allowed, string label, List<string> e)
        {
            if (text == null) return;
            foreach (Match m in Regex.Matches(text, @"\{([^{}]+)\}")) if (!allowed.Contains(m.Groups[1].Value)) e.Add(label + "包含未支持占位符：" + m.Value);
        }
    }
}
