using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace JU.GameData
{
    public enum SimulationPolicy { FollowRules, AlwaysRelease, AlwaysReject }
    public class SimulationDay
    {
        public int day, applicants, releases, rejections, compliant, violations, trust;
        public float purity, order, rawPurityDelta, rawOrderDelta, actualPurityDelta, actualOrderDelta;
        public bool trustBoundary, purityBoundary, orderBoundary;
    }
    public class SimulationReport
    {
        public int seed;
        public string version;
        public SimulationPolicy policy;
        public List<SimulationDay> days = new List<SimulationDay>();
        public string Summary()
        {
            if (days.Count == 0) return "暂无模拟结果。";
            var last = days[days.Count - 1];
            return policy + " / 种子" + seed + " / " + days.Count + "天 / " + days.Sum(d => d.applicants) + "名申请者" +
                "\n合规=" + days.Sum(d => d.compliant) + "；违规=" + days.Sum(d => d.violations) +
                "；最终信任=" + last.trust + "；最终灵墟/废墟=" + last.purity + "/" + last.order +
                "\n信任触及边界天数=" + days.Count(d => d.trustBoundary) + "；两界触及边界天数=" + days.Count(d => d.purityBoundary || d.orderBoundary) +
                "。本版未开启失败判定，触边后继续记录。";
        }
    }
    public static class SimulationRunner
    {
        /// <summary>新建独立局，不推进调试窗口当前局；相同种子下三种策略使用相同的申请者序列。</summary>
        public static SimulationReport Run(GameConfigData c, int seed, int days, SimulationPolicy policy)
        {
            if (days < 1 || days > 365 || !Enum.IsDefined(typeof(SimulationPolicy), policy)) throw new ArgumentException("模拟天数须为1～365，策略须有效。");
            var report = new SimulationReport { seed = seed, policy = policy, version = c.version };
            var session = GameSession.StartNewGame(c, seed);
            for (int i = 0; i < days; i++)
            {
                var today = session.GetState();
                foreach (var n in today.npcs)
                {
                    var rule = session.PreviewRule(n.id);
                    if (!rule.success || (policy == SimulationPolicy.FollowRules && !rule.applicable)) throw new InvalidOperationException("无法按规则模拟：" + rule.error);
                    bool approve = policy == SimulationPolicy.AlwaysRelease || (policy == SimulationPolicy.FollowRules && rule.requiredDecision != Decision.Reject);
                    var decision = session.SubmitApplication(n.id, approve);
                    if (!decision.success) throw new InvalidOperationException(decision.error);
                }
                var result = session.EndDay(today.day);
                if (!result.success) throw new InvalidOperationException(result.error);
                report.days.Add(new SimulationDay { day = result.settledDay, applicants = today.todayApplicantCount,
                    releases = result.approvals.Count(a => a.decision != Decision.Reject), rejections = result.approvals.Count(a => a.decision == Decision.Reject),
                    compliant = result.compliantCount, violations = result.violationCount, trust = result.after.trust, purity = result.after.purity, order = result.after.order,
                    rawPurityDelta = result.recordedPurityChange, rawOrderDelta = result.recordedOrderChange,
                    actualPurityDelta = result.actualPurityChange, actualOrderDelta = result.actualOrderChange,
                    trustBoundary = result.trustAtBoundary, purityBoundary = result.purityAtBoundary, orderBoundary = result.orderAtBoundary });
                // 模拟消费已到期反馈，验证正常队列路径，避免比较策略时积累无用文本。
                foreach (var f in session.GetAvailableFeedback()) session.ConsumeFeedback(f.id);
            }
            return report;
        }
        public static string ToCsv(IEnumerable<SimulationReport> reports)
        {
            var b = new StringBuilder("version,seed,policy,day,applicants,releases,rejections,compliant,violations,trust,utopia,anti_utopia,raw_utopia_delta,raw_anti_utopia_delta,actual_utopia_delta,actual_anti_utopia_delta,trust_boundary,utopia_boundary,anti_utopia_boundary\r\n");
            foreach (var r in reports) foreach (var d in r.days)
                b.AppendLine(string.Join(",", new[] { Cell(r.version), r.seed.ToString(CultureInfo.InvariantCulture), r.policy.ToString(),
                    d.day.ToString(), d.applicants.ToString(), d.releases.ToString(), d.rejections.ToString(), d.compliant.ToString(), d.violations.ToString(), d.trust.ToString(),
                    Number(d.purity), Number(d.order), Number(d.rawPurityDelta), Number(d.rawOrderDelta), Number(d.actualPurityDelta), Number(d.actualOrderDelta),
                    d.trustBoundary ? "1" : "0", d.purityBoundary ? "1" : "0", d.orderBoundary ? "1" : "0" }));
            return b.ToString();
        }
        private static string Number(float value) { return value.ToString("0.###", CultureInfo.InvariantCulture); }
        private static string Cell(string s) { return "\"" + (s ?? "").Replace("\"", "\"\"") + "\""; }
    }
}
