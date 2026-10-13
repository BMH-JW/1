using System;
using System.Collections.Generic;
using System.Linq;

namespace JU.GameData.Tests
{
    /// <summary>纯C#验收场景，同时用于Unity EditMode与独立验证。</summary>
    public static class GameDataScenarios
    {
        public static readonly string[] Names = {
            "DefaultConfig", "ValidationRejectsBrokenTables", "GenerationAndProfiles", "FixedSeed",
            "GenerationWeights", "LevelsAndDeceptionBoundaries", "FractionalImpact", "DecisionAndDailyAccounting",
            "ShelterRelease", "CorruptionAndDocumentSnapshot", "MutationBlocksRelease", "BlackSwanTiming",
            "BlackSwanExtraCorruption", "RecoveryCeiling", "TrustFailure", "WorldFailure",
            "RepeatedDayAndInvalidActions", "SnapshotsAndConfigIsolation", "UnprocessedApplicantsDepart"
        };
        public static void Run(string name, GameConfigData original)
        {
            var c = original.Copy();
            switch (name)
            {
                case "DefaultConfig":
                    Check(ConfigValidator.Validate(c).Count == 0, "默认配置须通过校验");
                    Check(c.tags.Count == 50 && c.details.Count == 25 && c.motives.Count == 9 && c.items.Count == 5, "原表数量");
                    Check(c.documents.Count == 17, "原表11条与补充6条文书");
                    break;
                case "ValidationRejectsBrokenTables":
                    c.bands[0].maxValue = -5;
                    Check(ConfigValidator.Validate(c).Count > 0, "检测区间重叠");
                    c = original.Copy(); c.tags.RemoveAt(0);
                    Check(ConfigValidator.Validate(c).Count > 0, "检测标签缺失");
                    c = original.Copy(); c.documents.RemoveAll(x => x.triggerType == 1 && x.field == "entry_reason");
                    Check(ConfigValidator.Validate(c).Count > 0, "检测文书缺失");
                    c = original.Copy(); c.details[0].id = c.details[1].id;
                    Check(ConfigValidator.Validate(c).Count > 0, "检测重复编号");
                    c = original.Copy(); c.bands[0].weight = float.NaN;
                    Check(ConfigValidator.Validate(c).Count > 0, "检测非有限权重");
                    bool threw = false;
                    try { GameSession.StartNewGame(c, 42); } catch (ArgumentException) { threw = true; }
                    Check(threw, "不启动无效配置");
                    break;
                case "GenerationAndProfiles":
                {
                    c.dailyApplicants = 1000;
                    var s = GameSession.StartNewGame(c, 42);
                    var npcs = s.GetState().npcs;
                    Check(npcs.Count == 1000 && npcs.Select(n => n.id).Distinct().Count() == 1000, "数量与唯一编号");
                    foreach (var n in npcs)
                    {
                        Check(n.attributes.memory >= -10 && n.attributes.memory <= 10 && n.attributes.relationship >= -10 && n.attributes.relationship <= 10 &&
                            n.attributes.desire >= -10 && n.attributes.desire <= 10 && n.attributes.will >= -10 && n.attributes.will <= 10, "四属性范围");
                        var p = s.GetProfile(n.id); var d = s.GetDocument(n.id);
                        Check(p.bondTag != null && p.driveTag != null && !string.IsNullOrWhiteSpace(p.bodyText), "档案查表");
                        Check(!string.IsNullOrWhiteSpace(d.bodyText) && !string.IsNullOrWhiteSpace(d.reasonText) && !d.reasonText.Contains("{"), "文书覆盖及占位符");
                        Check(n.motive == c.motives.Single(x => x.id == n.motiveId).name, "动机匹配");
                        if (n.deception == Deception.Honest) Check(n.realOrigin == n.declaredOrigin && p.bodyText == d.bodyText, "诚实申报");
                        else Check(n.realOrigin != n.declaredOrigin, "伪装原籍不同");
                    }
                    Check(npcs.Any(n => n.motive == "普通生活"), "存在普通动机");
                    Check(npcs.Any(n => n.requestedDestination == Destination.Utopia) && npcs.Any(n => n.requestedDestination == Destination.AntiUtopia), "两界申请");
                    break;
                }
                case "FixedSeed":
                {
                    var a = GameSession.StartNewGame(c, 123); var b = GameSession.StartNewGame(c, 123);
                    Check(Signature(a.GetState()) == Signature(b.GetState()), "同种子首次生成一致");
                    a.EndDay(1); b.EndDay(1);
                    Check(Signature(a.GetState()) == Signature(b.GetState()), "次日仍一致");
                    var other = GameSession.StartNewGame(c, 456);
                    Check(Signature(other.GetState()) != Signature(GameSession.StartNewGame(c, 123).GetState()), "异种子不同");
                    break;
                }
                case "GenerationWeights":
                {
                    c.dailyApplicants = 1000;
                    var s = GameSession.StartNewGame(c, 42);
                    var values = s.GetState().npcs.SelectMany(n => new[] { n.attributes.memory, n.attributes.relationship, n.attributes.desire, n.attributes.will }).ToList();
                    foreach (var band in c.bands)
                    {
                        double share = values.Count(v => v >= band.minValue && v <= band.maxValue) / (double)values.Count;
                        Check(Math.Abs(share - band.weight) < 0.035, "生成权重偏差：" + band.level);
                    }
                    break;
                }
                case "LevelsAndDeceptionBoundaries":
                {
                    var s = GameSession.StartNewGame(c, 42);
                    foreach (int x in new[] { -10, -6 }) Check(s.GetLevel(x) == 1, "等级1");
                    foreach (int x in new[] { -5, -1 }) Check(s.GetLevel(x) == 2, "等级2");
                    Check(s.GetLevel(0) == 3, "等级3");
                    foreach (int x in new[] { 1, 5 }) Check(s.GetLevel(x) == 4, "等级4");
                    foreach (int x in new[] { 6, 10 }) Check(s.GetLevel(x) == 5, "等级5");
                    Check(s.GetDeception(3) == Deception.Honest && s.GetDeception(4) == Deception.Embellished &&
                        s.GetDeception(8) == Deception.Embellished && s.GetDeception(9) == Deception.Deceptive, "欺骗分段");
                    var a = new NpcAttributes { memory = -4, relationship = 5, desire = 6, will = -2 };
                    Check(GameSession.CalculateDeviation(a, Destination.Utopia) == 10 && GameSession.CalculateDeviation(a, Destination.AntiUtopia) == 15, "偏差公式");
                    break;
                }
                case "FractionalImpact":
                {
                    var a = new NpcAttributes { memory = 1 };
                    Near(GameSession.CalculateImpact(a, Destination.Utopia), 0.1f, "保留小数");
                    a = new NpcAttributes { memory = 2, relationship = 3, desire = 4, will = 5 };
                    Near(GameSession.CalculateImpact(a, Destination.Utopia), -0.4f, "纯净公式");
                    Near(GameSession.CalculateImpact(a, Destination.AntiUtopia), -1f, "秩序公式");
                    break;
                }
                case "DecisionAndDailyAccounting":
                {
                    var s = GameSession.StartNewGame(c, 42); var before = s.GetState();
                    float purity = 0, order = 0; int good = 0, bad = 0;
                    for (int i = 0; i < before.npcs.Count; i++)
                    {
                        var n = before.npcs[i];
                        if (n.mutated) { s.SubmitDecision(n.id, Decision.Reject); continue; }
                        var decision = i % 2 == 0 ? Decision.ReleaseToUtopia : Decision.ReleaseToAntiUtopia;
                        var result = s.SubmitDecision(n.id, decision);
                        Check(result.success, "放行成功");
                        if (result.approval.compliant) good++; else bad++;
                        if (i % 2 == 0) purity += result.approval.worldImpact; else order += result.approval.worldImpact;
                        Check(!s.SubmitDecision(n.id, decision).success, "重复放行拒绝");
                    }
                    Near(s.GetState().purity, before.purity, "日终前不结算");
                    var r = s.EndDay(before.day);
                    Check(r.compliantCount == good && r.violationCount == bad, "合规统计");
                    Near(r.approvalPurityChange, purity, "汇总纯净"); Near(r.approvalOrderChange, order, "汇总秩序");
                    Check(r.after.trust == before.trust + good * 5 - bad * 10 + 5, "信任公式");
                    Near(r.after.purity, before.purity + purity + r.purityRecovery, "世界结算");
                    break;
                }
                case "ShelterRelease":
                {
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs.First(x => !x.mutated);
                    Check(s.SubmitDecision(n.id, Decision.Reject).success, "拒签入所");
                    Check(s.GetState().todayApprovals.Count == 0, "拒签不算放行");
                    Check(!s.SubmitDecision(n.id, Decision.Reject).success, "重复拒签拒绝");
                    s.EndDay(1);
                    Check(s.SubmitDecision(n.id, Decision.ReleaseToUtopia).success, "次日再次审批");
                    Check(s.GetNpc(n.id).status == NpcStatus.Released, "离开收容状态");
                    break;
                }
                case "CorruptionAndDocumentSnapshot":
                {
                    c.dailyApplicants = 1000;
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs.First(x => x.attributes.desire == 5 && !x.mutated);
                    var oldProfile = s.GetProfile(n.id); var oldDoc = s.GetDocument(n.id);
                    s.SubmitDecision(n.id, Decision.Reject); s.EndDay(1);
                    var changed = s.GetNpc(n.id);
                    Check(changed.attributes.memory == Math.Max(-10, n.attributes.memory-1) && changed.attributes.relationship == Math.Max(-10, n.attributes.relationship-1) &&
                        changed.attributes.desire == 6 && changed.attributes.will == Math.Min(10, n.attributes.will+1), "新入所当日腐化与限制");
                    Check(s.GetProfile(n.id).bodyText != oldProfile.bodyText, "真实档案刷新");
                    Check(s.GetDocument(n.id).bodyText == oldDoc.bodyText && s.GetDocument(n.id).reasonText == oldDoc.reasonText, "申报文书保留");
                    break;
                }
                case "MutationBlocksRelease":
                {
                    c.dailyApplicants = 1000;
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs.First(x => x.attributes.desire >= 6 && x.attributes.will >= 6 && !x.mutated);
                    s.SubmitDecision(n.id, Decision.Reject);
                    bool reported = false;
                    for (int day = 1; day <= 4; day++) { var r = s.EndDay(day); reported |= r.newlyMutatedIds.Contains(n.id); }
                    Check(s.GetNpc(n.id).mutated && reported, "腐化触发变异");
                    Check(!s.SubmitDecision(n.id, Decision.ReleaseToUtopia).success && !s.SubmitDecision(n.id, Decision.ReleaseToAntiUtopia).success, "变异不可放行");
                    Check(s.GetNpc(n.id).attributes.desire == 10 && s.GetNpc(n.id).attributes.will == 10, "属性最大10");
                    break;
                }
                case "BlackSwanTiming":
                {
                    c.initialPurity = c.initialOrder = 29; c.dailyRecovery = 0; c.worldFailureThreshold = -1000;
                    var s = GameSession.StartNewGame(c, 42); var first = s.EndDay(1);
                    Check(first.blackSwanStarted && !first.blackSwanApplied && first.after.blackSwanRemaining == 3, "触发日不扣值");
                    for (int day = 2; day <= 4; day++)
                    {
                        var r = s.EndDay(day);
                        Check(r.blackSwanApplied && !r.blackSwanStarted && r.after.blackSwanRemaining == 4-day, "连续3次且不重置");
                        Near(r.after.purity, 29 - (day-1)*10, "两界扣值"); Near(r.after.order, r.after.purity, "另一界也扣值");
                    }
                    var next = s.EndDay(5);
                    Check(next.blackSwanStarted && !next.blackSwanApplied, "事件结束次日可再触发");
                    break;
                }
                case "BlackSwanExtraCorruption":
                {
                    c.initialPurity = 29; c.dailyRecovery = 0; c.worldFailureThreshold = -1000;
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs.First(x => x.attributes.desire <= 5 && x.attributes.will <= 5);
                    s.SubmitDecision(n.id, Decision.Reject); s.EndDay(1);
                    var one = s.GetNpc(n.id).attributes; s.EndDay(2); var two = s.GetNpc(n.id).attributes;
                    Check(two.desire == Math.Min(10, one.desire+3) && two.will == Math.Min(10, one.will+3), "额外腐化+2与基础+1");
                    Check(two.memory == Math.Max(-10, one.memory-1), "记忆不额外腐化");
                    break;
                }
                case "RecoveryCeiling":
                {
                    c.initialPurity = 49.8f; c.initialOrder = 51;
                    var r = GameSession.StartNewGame(c, 42).EndDay(1);
                    Near(r.after.purity, 50, "恢复不超过50"); Near(r.after.order, 51, "高于50不截断");
                    Near(r.purityRecovery, 0.2f, "恢复差额"); Near(r.orderRecovery, 0, "无需恢复");
                    break;
                }
                case "TrustFailure":
                {
                    c.initialTrust = 1; c.violationPenalty = 100;
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs.First(x => GameSession.CalculateImpact(x.attributes, Destination.Utopia) < 0 && !x.mutated);
                    s.SubmitDecision(n.id, Decision.ReleaseToUtopia); var r = s.EndDay(1);
                    Check(r.after.gameOver && r.after.trust <= 0 && r.after.day == 1, "信任失败停止推进");
                    Check(!s.EndDay(1).success && !s.SubmitDecision(r.after.npcs.First().id, Decision.Reject).success, "结束后禁止操作");
                    break;
                }
                case "WorldFailure":
                {
                    c.initialPurity = c.initialOrder = 30; c.dailyRecovery = 0;
                    var s = GameSession.StartNewGame(c, 42); s.EndDay(1); var r = s.EndDay(2);
                    Check(r.after.gameOver && r.after.failureReasons.Count == 2, "两界20失败并记录所有原因");
                    Near(r.after.purity, 20, "失败边界");
                    break;
                }
                case "RepeatedDayAndInvalidActions":
                {
                    var s = GameSession.StartNewGame(c, 42); var before = Signature(s.GetState());
                    Check(!s.SubmitDecision("missing", Decision.Reject).success && !s.SubmitDecision(s.GetState().npcs[0].id, (Decision)99).success, "非法操作");
                    Check(!s.EndDay(0).success && Signature(s.GetState()) == before, "错误天数无副作用");
                    s.EndDay(1); before = Signature(s.GetState());
                    Check(!s.EndDay(1).success && Signature(s.GetState()) == before, "防止重复日终");
                    break;
                }
                case "SnapshotsAndConfigIsolation":
                {
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs[0]; var before = Signature(s.GetState());
                    var state = s.GetState(); state.trust = -99; state.npcs[0].attributes.desire = 999;
                    s.GetNpc(n.id).attributes.memory = 999; s.GetProfile(n.id).bondTag.name = "改变";
                    c.names.Clear(); c.tags.Clear(); c.bands.Clear();
                    Check(Signature(s.GetState()) == before, "外部快照不改变内部数据");
                    Check(s.GetProfile(n.id).bondTag.name != "改变", "配置条目不泄漏");
                    Check(s.EndDay(1).success, "修改原配置不影响当前局");
                    break;
                }
                case "UnprocessedApplicantsDepart":
                {
                    var s = GameSession.StartNewGame(c, 42); var old = s.GetState().npcs.Select(x => x.id).ToList(); var r = s.EndDay(1);
                    Check(r.compliantCount == 0 && r.violationCount == 0 && r.after.trust == 55, "未审批不处罚");
                    Check(r.after.npcs.Count == 8 && r.after.npcs.All(x => !old.Contains(x.id)), "旧队列离开，新队列替换");
                    Check(!s.SubmitDecision(old[0], Decision.Reject).success, "离开的NPC不能再审批");
                    break;
                }
                default: throw new ArgumentException("未知测试场景：" + name);
            }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Near(float actual, float expected, string message) { Check(Math.Abs(actual-expected) < 0.0001, message + ": " + actual + " != " + expected); }
        private static string Signature(GameState s)
        {
            return s.day + "/" + s.trust + "/" + s.purity + "/" + s.order + "/" + s.blackSwanRemaining + "/" + s.gameOver + "/" +
                string.Join("|", s.npcs.Select(n => n.id + "," + n.name + "," + n.realOrigin + "," + n.declaredOrigin + "," + n.attributes.memory + "," +
                    n.attributes.relationship + "," + n.attributes.desire + "," + n.attributes.will + "," + n.motive + "," + n.requestedDestination + "," +
                    n.bodyDeclaration + "," + n.reasonDeclaration + "," + n.status + "," + n.mutated));
        }
    }
}
