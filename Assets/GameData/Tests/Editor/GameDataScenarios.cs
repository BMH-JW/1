using System;
using System.Globalization;
using System.Linq;

namespace JU.GameData.Tests
{
    public static class GameDataScenarios
    {
        public static readonly string[] Names = {
            "ReplyBaseline", "ReleaseGate", "FixedSeed", "DailyCounts", "GenerationWeights", "NoReroll", "SourcesClaims", "UniqueNames", "IdentityContinuity",
            "ExplicitDeclaration", "SpecialTemplatePriority", "SixPlannerTemplates", "BodyFromDesire", "PlayerPrivacy", "MemoryMapping",
            "BinaryCompliance", "NoRetargetOrDetain", "RejectNotShelter", "DeferredWorld", "RejectSourceImpact", "NewWorldFormula",
            "DailyRounding", "MidpointRounding", "TrustIncludesReject", "NoDailyBonus", "TrustAndWorldClamp", "NoImplicitFailure",
            "MechanismGates", "UnsupportedMechanisms", "FeedbackDelay", "FeedbackOnce", "FeedbackPayload", "SnapshotIsolation",
            "MalformedConfig", "MissingDocuments", "MissingNextDayAtomic", "RuleConflictAtomic", "MultiDayPolicies", "CsvCulture", "PortraitPools"
        };
        public static void Run(string name, GameConfigData source)
        {
            var c = source.Copy();
            switch (name)
            {
                case "ReplyBaseline":
                    Check(ConfigValidator.ValidateForPlay(c).Count == 0, string.Join(";", ConfigValidator.ValidateForPlay(c)));
                    Check(c.testConfiguration && c.minDailyApplicants == 4 && c.maxDailyApplicants == 8 && c.documents.Count == 17, "当前策划回复测试基线");
                    Check(!c.formulas.Single(f => f.id == "F003").enabled && c.complianceReward == 3 && c.violationPenalty == 5, "旧算法关闭，信任明确测试值"); break;
                case "ReleaseGate":
                    Check(ConfigValidator.ValidateForRelease(c).Count > 0, "测试人物/立绘/公式不能冒充正式发布"); break;
                case "FixedSeed":
                {
                    var a = GameSession.StartNewGame(c, 42); var b = GameSession.StartNewGame(c, 42);
                    Check(Signature(a) == Signature(b), "首日复现"); a.EndDay(1); b.EndDay(1); Check(Signature(a) == Signature(b), "次日复现"); break;
                }
                case "DailyCounts":
                {
                    var s = GameSession.StartNewGame(c, 42); var counts = new System.Collections.Generic.HashSet<int>();
                    for (int day = 1; day <= 80; day++) { int n = s.GetState().todayApplicantCount; counts.Add(n); Check(n >= 4 && n <= 8, "4～8含边界"); Check(s.EndDay(day).success, "日结成功"); }
                    Check(counts.SetEquals(new[] {4,5,6,7,8}), "多个不同人数均可出现"); break;
                }
                case "GenerationWeights":
                {
                    c.minDailyApplicants=c.maxDailyApplicants=8; var s=GameSession.StartNewGame(c,42); var values=new System.Collections.Generic.List<int>();
                    for(int day=1;day<=100;day++){values.AddRange(s.GetState().npcs.SelectMany(n=>new[]{n.attributes.memory,n.attributes.relationship,n.attributes.desire,n.attributes.will}));s.EndDay(day);}
                    foreach(var b in c.bands){double ratio=values.Count(v=>v>=b.minValue && v<=b.maxValue)/(double)values.Count;Check(Math.Abs(ratio-c.generationWeights.Single(w=>w.level==b.level).weight)<0.04,"表6权重独立用于四属性");}break;
                }
                case "NoReroll":
                { var s = GameSession.StartNewGame(c, 4); string before = Signature(s); for (int i = 0; i < 20; i++) { s.GetState(); s.GetDocument(s.GetState().npcs[0].id); } Check(Signature(s) == before, "反复开面板不重抽"); break; }
                case "SourcesClaims":
                {
                    var s = GameSession.StartNewGame(c, 9); int utopia = 0, total = 0; bool uneven = false;
                    for (int d = 1; d <= 100; d++)
                    {
                        var ns = s.GetState().npcs; int u = ns.Count(n => n.sourceWorld == Destination.Utopia); utopia += u; total += ns.Count; uneven |= u * 2 != ns.Count;
                        Check(ns.All(n => n.sourceWorld != n.requestedDestination), "目的地严格为来源另一界"); s.EndDay(d);
                    }
                    Check(uneven && utopia > total * 0.35 && utopia < total * 0.65, "独立随机，无每日均分配额"); break;
                }
                case "UniqueNames":
                { var s = GameSession.StartNewGame(c, 1); for (int d = 1; d <= 30; d++) { var ns = s.GetState().npcs; Check(ns.Select(n => n.name).Distinct().Count() == ns.Count, "单日姓名不重复"); s.EndDay(d); } break; }
                case "IdentityContinuity":
                {
                    c.minDailyApplicants = c.maxDailyApplicants = c.identities.Count;
                    var s = GameSession.StartNewGame(c, 17); var first = s.GetState().npcs.ToDictionary(n => n.identityId);
                    s.EndDay(1); foreach (var n in s.GetState().npcs) { var old = first[n.identityId]; Check(n.id != old.id && n.name == old.name && n.realOrigin == old.realOrigin && n.portraitKey == old.portraitKey, "身份固定，申请ID独立"); } break;
                }
                case "ExplicitDeclaration":
                {
                    c = Fixed(c, -10); foreach (var p in c.identities) { p.declarationType = Deception.Honest; p.motiveId = 9; }
                    var s = GameSession.StartNewGame(c, 42); Check(s.GetState().npcs.All(n => n.deception == Deception.Honest && n.motiveId == 9), "不按旧偏差与极端属性重算类型或动机"); break;
                }
                case "SpecialTemplatePriority":
                {
                    foreach (var p in c.identities) { p.declarationType = Deception.Deceptive; p.motiveId = 4; }
                    var s = GameSession.StartNewGame(c, 42);
                    foreach (var n in s.GetState().npcs) Check(s.GetDocument(n.id).reasonText.Contains(n.requestedDestination == Destination.Utopia ? "失散的爱人" : "统一调配"), "明确动机命中特殊文书"); break;
                }
                case "SixPlannerTemplates":
                {
                    var general = c.documents.Where(d => d.id >= 9001 && d.id <= 9006).ToList();
                    Check(general.Count == 6 && general.All(d => !d.text.Contains("演示补充")), "真实策划通用文案");
                    c.minDailyApplicants = c.maxDailyApplicants = 8; var seen = new System.Collections.Generic.HashSet<string>(); var s = GameSession.StartNewGame(c, 42);
                    for (int day = 1; day <= 15; day++) { foreach (var n in s.GetState().npcs) { var doc = s.GetDocument(n.id); Check(!doc.reasonText.Contains("{") && !doc.reasonText.Contains("乌托邦"), "占位符及名称正确"); seen.Add(n.deception + "/" + n.requestedDestination); } s.EndDay(day); }
                    Check(seen.Count == 6, "三类型乘两个目的地全部生成"); break;
                }
                case "BodyFromDesire":
                {
                    c = Fixed(c, 0); foreach (var p in c.identities) { p.fixedAttributes.desire = 10; p.declarationType = Deception.Honest; }
                    var s = GameSession.StartNewGame(c, 5); foreach (var n in s.GetState().npcs) Check(s.GetDocument(n.id).bodyText == c.details.Single(d => d.attribute == "desire_body" && d.level == 5).text, "诚实身体按欲望等级生成"); break;
                }
                case "PlayerPrivacy":
                {
                    Check(typeof(GameSession).GetMethod("Investigate") == null && typeof(GameConfigData).GetField("dailyInvestigationQuota") == null, "移除调查接口和参数");
                    Check(typeof(PlayerProfile).GetField("realOrigin") == null && typeof(PlayerProfile).GetField("motive") == null, "普通档案不泄露原籍/隐藏动机");
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs[0]; var p = s.GetPlayerProfile(n.id);
                    Check(!string.IsNullOrEmpty(p.bondTraceSentence) && p.relevantAttributes.SequenceEqual(new[] { "memory" }), "两描述保留、只突出记忆"); break;
                }
                case "MemoryMapping":
                    for (int memory = -10; memory <= 10; memory++)
                    {
                        var e = RuleEngine.Evaluate(c, RuleEngine.Select(c, 1), new NpcAttributes { memory = memory });
                        Check(e.success && e.target == (memory < 0 ? Decision.ReleaseToAntiUtopia : Decision.ReleaseToUtopia), "全21点记忆映射，0去灵虚");
                    }
                    break;
                case "BinaryCompliance":
                {
                    c = Fixed(c, 0); var s = Batch(c, Destination.Utopia, 1); var ns = s.GetState().npcs;
                    foreach (var n in ns)
                    { var rule = s.PreviewRule(n.id); Check(rule.requiredDecision == (n.requestedDestination == Destination.Utopia ? Decision.ReleaseToUtopia : Decision.Reject), "申报与准入目标比较"); Check(s.SubmitApplication(n.id, rule.requiredDecision != Decision.Reject).approval.compliant, "正确放行或拒签均合规"); }
                    break;
                }
                case "NoRetargetOrDetain":
                {
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs[0]; string before = Signature(s);
                    var other = n.requestedDestination == Destination.Utopia ? Decision.ReleaseToAntiUtopia : Decision.ReleaseToUtopia;
                    Check(!s.SubmitDecision(n.id, other).success && !s.SubmitDecision(n.id, Decision.Detain).success && Signature(s) == before, "改派/扣留不产生副作用"); break;
                }
                case "RejectNotShelter":
                {
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs[0];
                    Check(s.SubmitApplication(n.id, false).success && s.GetNpc(n.id).status == NpcStatus.Rejected, "拒签结束申请而非收容");
                    Check(s.GetNpc(n.id).attributes.memory == n.attributes.memory && !s.SubmitApplication(n.id, true).success, "不减记忆，不重复审批");
                    s.EndDay(1); Check(s.GetNpc(n.id) == null && !s.GetState().npcs.Any(x => x.status == NpcStatus.Sheltered), "次日只保留新申请"); break;
                }
                case "DeferredWorld":
                {
                    var s = GameSession.StartNewGame(c, 42); var before = s.GetState(); var r = s.SubmitApplication(before.npcs[0].id, false);
                    Check(r.success && s.GetState().purity == before.purity && s.GetState().trust == before.trust && s.GetState().feedbackQueue.Count == 0, "审批仅记账");
                    Check(r.immediateFeedback == "处理已记录。", "默认不提示隐藏合规答案"); break;
                }
                case "RejectSourceImpact":
                {
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs[0]; var r = s.SubmitApplication(n.id, false).approval;
                    Near(r.purityDelta, n.sourceWorld == Destination.Utopia ? -1 : 0, "拒签灵虚记账"); Near(r.orderDelta, n.sourceWorld == Destination.AntiUtopia ? -1 : 0, "拒签废墟记账");
                    var end = s.EndDay(1); Near(end.after.purity, 50+r.purityDelta, "来源扣除日终生效"); break;
                }
                case "NewWorldFormula":
                {
                    c = Fixed(c, -5); var s = GameSession.StartNewGame(c, 42);
                    foreach (var n in s.GetState().npcs) { var a = s.SubmitApplication(n.id, true).approval; Near(a.purityDelta, n.requestedDestination == Destination.Utopia ? -1 : 0, "M/5"); Near(a.orderDelta, n.requestedDestination == Destination.AntiUtopia ? 1 : 0, "-M/5"); }
                    break;
                }
                case "DailyRounding":
                {
                    c = Fixed(c, 1); var s = Batch(c, Destination.Utopia, 3);
                    foreach (var n in s.GetState().npcs.Where(n => n.requestedDestination == Destination.Utopia).Take(3)) s.SubmitApplication(n.id, true);
                    var r = s.EndDay(1); Near(r.recordedPurityChange, 0.6f, "先累计0.2三次"); Near(r.after.purity, 51, "统一取整，不逐人归零"); break;
                }
                case "MidpointRounding":
                {
                    c = Fixed(c, -1); c.impactDivisor = 2; var s = Batch(c, Destination.Utopia, 1);
                    s.SubmitApplication(s.GetState().npcs.First(n => n.requestedDestination == Destination.Utopia).id, true);
                    Near(s.EndDay(1).after.purity, 50, "50-0.5=49.5，四舍五入50，不先对变化-0.5取整");
                    c = Fixed(c, 1); s = Batch(c, Destination.Utopia, 1); s.SubmitApplication(s.GetState().npcs.First(n => n.requestedDestination == Destination.Utopia).id, true); Near(s.EndDay(1).after.purity, 51, "50.5取51而非银行家舍入50"); break;
                }
                case "TrustIncludesReject":
                {
                    c = Fixed(c, 0); var s = Batch(c, Destination.AntiUtopia, 1); var n = s.GetState().npcs.First(x => x.requestedDestination == Destination.AntiUtopia);
                    Check(s.SubmitApplication(n.id, false).approval.compliant, "不符合申报时合规拒签"); var r = s.EndDay(1);
                    Check(r.compliantCount == 1 && r.after.trust == 53, "拒签也+3，无额外+5"); break;
                }
                case "NoDailyBonus":
                { var s = GameSession.StartNewGame(c, 42); var r = s.EndDay(1); Check(r.after.trust == 50 && r.unprocessedCount == r.before.todayApplicantCount, "空日无固定奖励，未审批不自动拒签"); break; }
                case "TrustAndWorldClamp":
                {
                    c = Fixed(c, 10); c.initialPurity = 99; c.initialTrust = 99; var s = Batch(c, Destination.Utopia, 1); var n = s.GetState().npcs.First(x => x.requestedDestination == Destination.Utopia); s.SubmitApplication(n.id, true); var r = s.EndDay(1); Near(r.after.purity, 100, "世界上限"); Check(r.after.trust == 100 && r.trustChange == 1, "信任限幅后实际变化");
                    c.initialPurity = 1; c.initialTrust = 1; s = Batch(c, Destination.AntiUtopia, 1); n = s.GetState().npcs.First(x => x.requestedDestination == Destination.AntiUtopia); s.SubmitApplication(n.id, true); r = s.EndDay(1); Near(r.after.order, 48, "负影响正确"); Check(r.after.trust == 0, "信任下限"); break;
                }
                case "NoImplicitFailure":
                { c.initialTrust = 0; c.initialPurity = 0; c.initialOrder = 100; var s = GameSession.StartNewGame(c, 42); Check(s.EndDay(1).success && s.EndDay(2).success, "边界不触发旧失败规则"); break; }
                case "MechanismGates":
                {
                    c.enableWorldChanges = false; c.enableDelayedFeedback = false; c.enableDocuments = false; c.enableBondTrace = false; c.enableDriveAssessment = false;
                    var s = GameSession.StartNewGame(c, 42); var n = s.GetState().npcs[0]; Check(s.GetDocument(n.id)==null && s.GetPlayerProfile(n.id).bondTraceSentence=="", "关闭生成不返回内容");
                    s.SubmitApplication(n.id,false); var r=s.EndDay(1); Check(r.after.purity==50 && r.after.order==50 && r.after.feedbackQueue.Count==0, "总开关覆盖拒签和反馈"); break;
                }
                case "UnsupportedMechanisms":
                    foreach (string id in new[] {"F003","F006","F007","F008","F009","F010","F011","F012","F014","F015"}) { c=source.Copy();var f=c.formulas.Single(x=>x.id==id);f.enabled=f.confirmed=true;Check(ConfigValidator.ValidateForPlay(c).Count>0,"暂停公式不可恢复旧效果 "+id); }
                    c=source.Copy();c.enablePopulationFeedback=true;Check(ConfigValidator.Validate(c).Count>0,"未定权重不能打开");break;
                case "FeedbackDelay":
                {
                    c.feedbackDelayDays=2;var s=GameSession.StartNewGame(c,42);s.SubmitApplication(s.GetState().npcs[0].id,false);s.EndDay(1);var f=s.GetState().feedbackQueue.Single();Check(s.GetAvailableFeedback().Count==0 && !s.ConsumeFeedback(f.id),"不可提前消费");s.EndDay(2);Check(s.GetAvailableFeedback().Count==1,"第三天可用");break;
                }
                case "FeedbackOnce":
                {
                    var s=GameSession.StartNewGame(c,42);s.SubmitApplication(s.GetState().npcs[0].id,false);var r=s.EndDay(1);var f=s.GetAvailableFeedback().Single();Check(s.ConsumeFeedback(f.id) && !s.ConsumeFeedback(f.id),"只消费一次");string before=Signature(s);Check(!s.EndDay(1).success && Signature(s)==before && s.GetState().feedbackQueue.Count==1,"重复日结不重复影响或入队");break;
                }
                case "FeedbackPayload":
                {
                    c=Fixed(c,1);c.initialPurity=100;var s=Batch(c,Destination.Utopia,1);s.SubmitApplication(s.GetState().npcs.First(n=>n.requestedDestination==Destination.Utopia).id,true);var r=s.EndDay(1);var f=s.GetAvailableFeedback().Single();Near(f.recordedPurityDelta,0.2f,"单次记账独立");Near(f.actualPurityChange,0,"实际受限幅");Check(f.settlementId==r.settlementId && f.sourceDecisionId==r.approvals[0].id && !f.text.Contains("{"),"反馈可追溯且占位符解析");break;
                }
                case "SnapshotIsolation":
                {
                    var s=GameSession.StartNewGame(c,42);string before=Signature(s);s.GetState().npcs[0].attributes.memory=999;s.GetState().identities[0].portraitKey="changed";s.GetConfigSnapshot().identities.Clear();c.documents.Clear();Check(Signature(s)==before && s.EndDay(1).success,"快照与原配置不影响当前局");break;
                }
                case "MalformedConfig":
                    c.rules[0].repeatEveryDays=0;Check(ConfigValidator.Validate(c).Count>0,"防除零");c=source.Copy();c.generationWeights[0].weight=float.NaN;Check(ConfigValidator.Validate(c).Count>0,"无效权重");c=source.Copy();c.identities[0].name=c.identities[1].name;Check(ConfigValidator.Validate(c).Count>0,"防重名");c=source.Copy();c.portraitPools.Add(c.portraitPools[0].Copy());Check(ConfigValidator.Validate(c).Count>0,"重复池报告而不抛异常");break;
                case "MissingDocuments":
                    c.documents.RemoveAll(d=>d.field=="entry_reason" && d.triggerType==1);Check(ConfigValidator.ValidateForPlay(c).Any(e=>e.Contains("文书缺少覆盖")),"不回退或随机补文案");break;
                case "MissingNextDayAtomic":
                { c.rules[0].lastDay=1;var s=GameSession.StartNewGame(c,42);s.SubmitApplication(s.GetState().npcs[0].id,false);string before=Signature(s);Check(!s.EndDay(1).success && Signature(s)==before,"次日无规则不半结算");break; }
                case "RuleConflictAtomic":
                { var other=c.rules[0].Copy();other.id="CONFLICT";other.lowDecision=Decision.ReleaseToAntiUtopia;other.highDecision=Decision.ReleaseToUtopia;c.rules.Add(other);c.dailyRuleCount=2;var s=GameSession.StartNewGame(c,42);string before=Signature(s);Check(!s.SubmitApplication(s.GetState().npcs[0].id,true).success && Signature(s)==before,"冲突审批无副作用");break; }
                case "MultiDayPolicies":
                {
                    var good=SimulationRunner.Run(c,42,30,SimulationPolicy.FollowRules);var release=SimulationRunner.Run(c,42,30,SimulationPolicy.AlwaysRelease);var reject=SimulationRunner.Run(c,42,30,SimulationPolicy.AlwaysReject);
                    Check(good.days.All(d=>d.violations==0) && good.days.Last().trust==100,"按规则策略全合规并触顶");
                    Check(good.days.Select(d=>d.applicants).SequenceEqual(release.days.Select(d=>d.applicants)) && good.days.Select(d=>d.applicants).SequenceEqual(reject.days.Select(d=>d.applicants)),"三策略共用同种子申请者序列");
                    Check(new[]{good,release,reject}.All(r=>r.days.All(d=>d.trust>=0 && d.trust<=100 && d.purity>=0 && d.purity<=100 && d.order>=0 && d.order<=100)),"多日范围正确");break;
                }
                case "CsvCulture":
                { var previous=CultureInfo.CurrentCulture;try { CultureInfo.CurrentCulture=new CultureInfo("fr-FR");var r=SimulationRunner.Run(c,2,2,SimulationPolicy.AlwaysRelease);var csv=SimulationRunner.ToCsv(new[]{r});Check(csv.Split('\n').Where(l=>l.Trim().Length>0).Count()==3 && csv.Contains("raw_utopia_delta"),"CSV字段和行数不受地区小数逗号影响"); }finally{CultureInfo.CurrentCulture=previous;}break; }
                case "PortraitPools":
                    c.identities[0].portraitKey="missing";Check(ConfigValidator.Validate(c).Any(e=>e.Contains("立绘")),"指定图片必须在原籍池");c=source.Copy();c.identities[0].declarationType=(Deception)99;Check(ConfigValidator.Validate(c).Count>0,"类型必须显式有效");break;
                default:throw new ArgumentException(name);
            }
        }
        private static GameConfigData Fixed(GameConfigData c,int memory)
        { c.minDailyApplicants=c.maxDailyApplicants=8;foreach(var p in c.identities){p.useFixedAttributes=true;p.fixedAttributes=new NpcAttributes{memory=memory};}return c; }
        private static GameSession Batch(GameConfigData c,Destination destination,int count)
        { for(int seed=0;seed<100;seed++){var s=GameSession.StartNewGame(c,seed);if(s.GetState().npcs.Count(n=>n.requestedDestination==destination)>=count)return s;}throw new Exception("测试未找到足够申请者"); }
        private static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        private static void Near(float a,float b,string message){Check(Math.Abs(a-b)<0.0001f,message+"："+a+" != "+b);}
        private static string Signature(GameSession session)
        {
            var s=session.GetState();return s.day+"/"+s.trust+"/"+s.purity+"/"+s.order+"/"+s.todayApprovals.Count+"/"+s.feedbackQueue.Count+"/"+
                string.Join("|",s.npcs.Select(n=>n.id+","+n.identityId+","+n.name+","+n.realOrigin+","+n.portraitKey+","+n.attributes.memory+","+n.attributes.relationship+","+n.attributes.desire+","+n.attributes.will+","+n.sourceWorld+","+n.requestedDestination+","+n.status));
        }
    }
}
