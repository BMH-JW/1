using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace JU.GameData
{
    /// <summary>独立内存局；编辑配置后重新开局。场景层不自行计算审批、世界或信任变化。</summary>
    public sealed class GameSession
    {
        private readonly GameConfigData config;
        private readonly Random random;
        private readonly GameState state;
        private int nextNpcId, nextDecisionId, lastSettledDay;
        public event Action<string> Diagnostic;
        private GameSession(GameConfigData input, int seed)
        {
            var errors = ConfigValidator.ValidateForPlay(input);
            if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors), "input");
            config = input.Copy(); random = new Random(seed);
            state = new GameState { day = config.startDay, seed = seed, trust = config.initialTrust, purity = config.initialPurity, order = config.initialOrder };
            foreach (var person in config.identities.OrderBy(p => p.id, StringComparer.Ordinal))
            {
                var pool = config.portraitPools.Single(p => p.origin == person.origin);
                string portrait = string.IsNullOrEmpty(person.portraitKey) ? pool.portraitKeys[random.Next(pool.portraitKeys.Count)] : person.portraitKey;
                state.identities.Add(new IdentitySnapshot { id = person.id, name = person.name, origin = person.origin, portraitKey = portrait });
            }
            InstallDay(RuleEngine.Select(config, state.day), GenerateApplicants(state.day));
        }
        public static GameSession StartNewGame(GameConfigData c, int seed) { return new GameSession(c, seed); }
        public GameState GetState() { return state.Copy(); }
        public GameConfigData GetConfigSnapshot() { return config.Copy(); }
        public string GetWorldName(Destination id) { return RuleEngine.WorldName(config, id); }
        public string GetChoiceName(Decision d) { return RuleEngine.ChoiceName(config, d); }
        public string GetRuleAnnouncement()
        {
            if (!config.enableDailyRules) return "每日规则判定已关闭，本日处理不评定合规。";
            return string.Join("\n\n", state.dailyRules.Select(r => RuleEngine.Announcement(config, r))) +
                "\n多规则策略：" + (config.conflictPolicy == RuleConflictPolicy.RequireAgreement ? "结论必须一致，冲突时不提交。" : "最高优先级优先，同级按规则ID排序。");
        }
        /// <summary>开发数据，含原籍与隐藏动机，不能直接绑定普通人物档案。</summary>
        public NpcData GetNpc(string id) { var n = Find(id); return n == null ? null : n.Copy(); }
        public string GetPortraitKey(string id) { var n = Find(id); return n == null ? null : n.portraitKey; }
        public NpcProfile GetProfile(string id)
        {
            var n = Find(id); if (n == null) return null;
            return new NpcProfile { id = n.id, name = n.name, realOrigin = n.realOrigin, motive = n.motive,
                memoryText = Detail("memory", n.attributes.memory), relationshipText = Detail("relationship", n.attributes.relationship),
                desireText = Detail("desire", n.attributes.desire), willText = Detail("will", n.attributes.will), bodyText = Detail("desire_body", n.attributes.desire),
                bondTag = config.enableBondTrace ? Tag("memory", "relationship", n.attributes) : null,
                driveTag = config.enableDriveAssessment ? Tag("desire", "will", n.attributes) : null };
        }
        public PlayerProfile GetPlayerProfile(string id)
        {
            var n = Find(id); if (n == null) return null;
            return new PlayerProfile { id = n.id,
                bondTraceSentence = config.enableBondTrace ? Tag("memory", "relationship", n.attributes).description : "",
                driveAssessmentSentence = config.enableDriveAssessment ? Tag("desire", "will", n.attributes).description : "",
                relevantAttributes = state.dailyRules.Select(r => r.attribute).Distinct().ToList() };
        }
        public TravelDocument GetDocument(string id)
        {
            var n = Find(id); if (n == null || !config.enableDocuments) return null;
            return new TravelDocument { id = n.id, name = n.name, declaredOrigin = n.declaredOrigin, sourceWorld = n.sourceWorld,
                requestedDestination = n.requestedDestination, sourceDisplayName = GetWorldName(n.sourceWorld), destinationDisplayName = GetWorldName(n.requestedDestination),
                bodyText = n.bodyDeclaration, reasonText = n.reasonDeclaration };
        }
        public RuleEvaluation PreviewRule(string id)
        {
            var n = Find(id); if (n == null) return new RuleEvaluation { error = "NPC不存在。" };
            var evaluation = RuleEngine.Evaluate(config, state.dailyRules, n.attributes);
            if (evaluation.success && evaluation.applicable)
            {
                var declaredAction = ReleaseChoice(n.requestedDestination);
                evaluation.requiredDecision = evaluation.target == declaredAction ? declaredAction : Decision.Reject;
                evaluation.traces.Add("申报=" + GetWorldName(n.requestedDestination) + "；本次应执行=" + GetChoiceName(evaluation.requiredDecision));
            }
            return evaluation;
        }
        public int GetLevel(int value)
        {
            if (value < -10 || value > 10) throw new ArgumentOutOfRangeException("value");
            return config.bands.Single(b => value >= b.minValue && value <= b.maxValue).level;
        }
        /// <summary>玩家只对当前申报作放行/拒签，不允许暗中改派目的地。</summary>
        public DecisionResult SubmitApplication(string id, bool approve)
        {
            var n = Find(id); if (n == null) return Rejected("NPC不存在。");
            return SubmitDecision(id, approve ? ReleaseChoice(n.requestedDestination) : Decision.Reject);
        }
        public DecisionResult SubmitDecision(string id, Decision choice)
        {
            if (choice == Decision.Detain) return Rejected("扣留未纳入本版；不能用拒签代替扣留。");
            if (!Enum.IsDefined(typeof(Decision), choice)) return Rejected("未知审批操作。");
            var n = Find(id);
            if (n == null || n.status != NpcStatus.Pending) return Rejected("申请不存在或已处理，不能重复审批。");
            if (choice != Decision.Reject && choice != ReleaseChoice(n.requestedDestination)) return Rejected("本版只允许按申报目的地放行，不能改派另一界。");
            var evaluation = PreviewRule(id); if (!evaluation.success) return Rejected(evaluation.error);
            decimal purity = 0, order = 0;
            if (config.enableWorldChanges)
            {
                if (choice == Decision.Reject)
                { if (n.sourceWorld == Destination.Utopia) purity = -config.rejectionSourcePenalty; else order = -config.rejectionSourcePenalty; }
                else if (choice == Decision.ReleaseToUtopia && Formula("F004")) purity = n.attributes.memory / (decimal)config.impactDivisor;
                else if (choice == Decision.ReleaseToAntiUtopia && Formula("F005")) order = -n.attributes.memory / (decimal)config.impactDivisor;
            }
            var record = new ApprovalRecord { id = "D-" + (++nextDecisionId).ToString("D6"), npcId = id, identityId = n.identityId, npcName = n.name,
                day = state.day, sourceWorld = n.sourceWorld, declaredDestination = n.requestedDestination, decision = choice,
                memoryAtDecision = n.attributes.memory, ruleTarget = evaluation.target, requiredDecision = evaluation.requiredDecision,
                complianceEvaluated = evaluation.applicable, compliant = evaluation.applicable && choice == evaluation.requiredDecision,
                ruleIds = string.Join(",", state.dailyRules.Select(r => r.id)), ruleTrace = string.Join("\n", evaluation.traces),
                purityDelta = (float)purity, orderDelta = (float)order, exactPurityDelta = purity, exactOrderDelta = order };
            state.todayApprovals.Add(record);
            n.status = choice == Decision.Reject ? NpcStatus.Rejected : NpcStatus.Released;
            if (choice != Decision.Reject) n.releasedDestination = n.requestedDestination;
            if (config.debugMode && config.logRuleResult && Diagnostic != null)
                foreach (Action<string> handler in Diagnostic.GetInvocationList()) { try { handler(record.ruleTrace); } catch { /* 开发日志订阅失败不能改变审批结果。 */ } }
            return new DecisionResult { success = true, approval = record.Copy(), immediateFeedback = config.showImmediateCompliance && record.complianceEvaluated ?
                (record.compliant ? "本次处理符合当日准入条款。" : "本次处理与当日条款不符。") : "处理已记录。" };
        }
        public List<FeedbackEvent> GetAvailableFeedback()
        { return state.feedbackQueue.Where(f => !f.consumed && f.availableDay <= state.day).Select(f => f.Copy<FeedbackEvent>()).ToList(); }
        public bool ConsumeFeedback(string id)
        {
            var f = state.feedbackQueue.SingleOrDefault(x => x.id == id);
            if (f == null || f.consumed || f.availableDay > state.day) return false;
            f.consumed = true; return true;
        }
        public DayEndReport EndDay(int expectedDay)
        {
            if (expectedDay != state.day || expectedDay <= lastSettledDay) return new DayEndReport { error = "天数不匹配；同一天只能结算一次。" };
            List<DailyRule> nextRules;
            try { nextRules = RuleEngine.Select(config, state.day + 1); }
            catch (Exception ex) { return new DayEndReport { error = "次日规则无效，本日未结算：" + ex.Message }; }
            // 准备阶段不写当前状态。配置/文书错误不得造成半次结算。
            List<NpcData> nextApplicants;
            try { nextApplicants = GenerateApplicants(state.day + 1); }
            catch (Exception ex) { return new DayEndReport { error = "次日生成失败，本日未结算：" + ex.Message }; }
            var report = new DayEndReport { success = true, settledDay = state.day, settlementId = "DAY-" + state.day,
                before = state.Copy(), approvals = state.todayApprovals.Select(a => a.Copy()).ToList(), unprocessedCount = state.npcs.Count(n => n.status == NpcStatus.Pending) };
            decimal rawPurity = state.todayApprovals.Sum(a => a.exactPurityDelta), rawOrder = state.todayApprovals.Sum(a => a.exactOrderDelta);
            report.recordedPurityChange = (float)rawPurity; report.recordedOrderChange = (float)rawOrder;
            float newPurity = SettleWorld(state.purity, rawPurity), newOrder = SettleWorld(state.order, rawOrder);
            foreach (var a in state.todayApprovals.Where(a => a.complianceEvaluated))
            { if (a.compliant) report.compliantCount++; else report.violationCount++; }
            int requestedTrust = Formula("F013") ? report.compliantCount * config.complianceReward - report.violationCount * config.violationPenalty : 0;
            int newTrust = (int)Math.Max(config.trustMin, Math.Min((long)config.trustMax, (long)state.trust + requestedTrust));
            report.actualPurityChange = newPurity - state.purity; report.actualOrderChange = newOrder - state.order;
            report.trustChange = newTrust - state.trust;
            report.trustAtBoundary = newTrust == 0 || newTrust == 100; report.purityAtBoundary = newPurity == 0 || newPurity == 100; report.orderAtBoundary = newOrder == 0 || newOrder == 100;
            var feedback = new List<FeedbackEvent>();
            if (config.enableDelayedFeedback) foreach (var a in state.todayApprovals)
            {
                var template = config.feedbackTemplates.Where(t => t.decision == a.decision && (t.compliance == -1 || (a.complianceEvaluated && t.compliance == (a.compliant ? 1 : 0))))
                    .OrderByDescending(t => t.compliance != -1).ThenBy(t => t.id, StringComparer.Ordinal).First();
                feedback.Add(new FeedbackEvent { id = "F-" + a.id, templateId = template.id, sourceDecisionId = a.id, settlementId = report.settlementId,
                    settledDay = state.day, availableDay = state.day + config.feedbackDelayDays, recordedPurityDelta = a.purityDelta, recordedOrderDelta = a.orderDelta,
                    actualPurityChange = report.actualPurityChange, actualOrderChange = report.actualOrderChange,
                    text = RenderFeedback(template.text, a, report) });
            }
            state.purity = newPurity; state.order = newOrder; state.trust = newTrust;
            state.feedbackQueue.AddRange(feedback); lastSettledDay = state.day;
            state.todayApprovals.Clear(); state.day++;
            InstallDay(nextRules, nextApplicants);

            // 游戏结束判定：秩序值或纯净值 < 20 失败，> 80 成功
            // 判定用 newPurity/newOrder（结算后的值，state.purity/order 已更新为这两个）
            string endReason = null;
            bool endWin = false;

            if (newOrder > 80f || newPurity > 80f)
            {
                endWin = true;
                var reasons = new List<string>();
                if (newOrder > 80f) reasons.Add($"秩序值 {newOrder:0} 超过 80");
                if (newPurity > 80f) reasons.Add($"纯净值 {newPurity:0} 超过 80");
                endReason = string.Join("；", reasons) + "。乌托邦秩序稳定，纯净之光普照。";
            }
            else if (newOrder < 20f || newPurity < 20f)
            {
                endWin = false;
                var reasons = new List<string>();
                if (newOrder < 20f) reasons.Add($"秩序值 {newOrder:0} 跌破 20");
                if (newPurity < 20f) reasons.Add($"纯净值 {newPurity:0} 跌破 20");
                endReason = string.Join("；", reasons) + "。社会分崩离析，世界走向毁灭。";
            }
            report.gameEnded = endReason != null;
            report.isWin = endWin;
            report.gameEndReason = endReason;
            if (report.gameEnded)
            {
                report.log.Add("===== 游戏结束 =====");
                report.log.Add($"结果：{(endWin ? "成功（乌托邦达成）" : "失败（世界崩溃）")}");
                report.log.Add($"原因：{endReason}");
            }
            else
            {
                report.log.Add("秩序/纯净未触及阈值（20~80 为安全区间），游戏继续。");
            }
            report.log.Add("本日申请者=" + report.before.todayApplicantCount + "；合规处理=" + report.compliantCount + "；违规处理=" + report.violationCount + "；未处理离开=" + report.unprocessedCount);
            report.log.Add("精确累计影响=" + rawPurity.ToString(CultureInfo.InvariantCulture) + "/" + rawOrder.ToString(CultureInfo.InvariantCulture) + "；日终取整并限幅后实际变化=" + Number(report.actualPurityChange) + "/" + Number(report.actualOrderChange));
            report.log.Add("信任实际变化=" + report.trustChange + "；无每日固定加分；拒签已计入合规统计。");
            report.log.Add("边界触发：秩序/纯净触及阈值时结束本版会话（20 以下失败 / 80 以上成功）。");
            report.after = state.Copy(); return report;
        }
        private List<NpcData> GenerateApplicants(int day)
        {
            int count = random.Next(config.minDailyApplicants, config.maxDailyApplicants + 1);
            var candidates = state.identities.ToList();
            // 部分Fisher-Yates，无放回抽样，单日不重复人物或姓名。
            for (int i = 0; i < count; i++) { int j = random.Next(i, candidates.Count); var t = candidates[i]; candidates[i] = candidates[j]; candidates[j] = t; }
            var result = new List<NpcData>();
            for (int i = 0; i < count; i++)
            {
                var identity = candidates[i]; var definition = config.identities.Single(x => x.id == identity.id);
                var a = definition.useFixedAttributes ? definition.fixedAttributes.Copy() : new NpcAttributes { memory = Roll(), relationship = Roll(), desire = Roll(), will = Roll() };
                var source = (Destination)random.Next(2); // 逐人等概率，不设置每日配额。
                var destination = source == Destination.Utopia ? Destination.AntiUtopia : Destination.Utopia;
                var motive = config.motives.Single(m => m.id == definition.motiveId);
                var n = new NpcData { id = "NPC-" + (nextNpcId + i + 1).ToString("D6"), identityId = identity.id, name = identity.name,
                    realOrigin = identity.origin, declaredOrigin = identity.origin, portraitKey = identity.portraitKey, sourceWorld = source,
                    requestedDestination = destination, generatedDay = day, attributes = a, motiveId = motive.id, motive = motive.name,
                    deception = definition.declarationType, status = NpcStatus.Pending };
                if (config.enableDocuments)
                { n.bodyDeclaration = n.deception == Deception.Honest ? Detail("desire_body", a.desire) : Document("body_state", n); n.reasonDeclaration = Document("entry_reason", n); }
                result.Add(n);
            }
            return result;
        }
        private void InstallDay(List<DailyRule> rules, List<NpcData> applicants)
        { state.dailyRules = rules; state.npcs = applicants; state.todayApplicantCount = applicants.Count; nextNpcId += applicants.Count; }
        private int Roll()
        {
            double roll = random.NextDouble(), sum = 0;
            var weights = config.generationWeights.OrderBy(w => w.level).ToList();
            var chosen = weights.Last(w => w.weight > 0);
            foreach (var w in weights) { sum += w.weight; if (roll < sum) { chosen = w; break; } }
            var b = config.bands.Single(x => x.level == chosen.level); return random.Next(b.minValue, b.maxValue + 1);
        }
        private string Document(string field, NpcData n)
        {
            string destination = n.requestedDestination == Destination.Utopia ? "utopia" : "anti_utopia";
            var row = config.documents.Where(d => d.field == field && d.triggerType == (int)n.deception && (d.motive == "any" || d.motive == n.motive) && (d.destination == "any" || d.destination == destination))
                .OrderByDescending(d => (d.motive == "any" ? 0 : 2) + (d.destination == "any" ? 0 : 1)).ThenBy(d => d.id).First();
            return row.text.Replace("反乌托邦", "{anti_utopia}").Replace("乌托邦", "{utopia}")
                .Replace("{motive}", n.motive).Replace("{motive_desc}", config.motives.Single(m => m.id == n.motiveId).description)
                .Replace("{name}", n.name).Replace("{origin}", n.declaredOrigin).Replace("{destination}", GetWorldName(n.requestedDestination))
                .Replace("{utopia}", GetWorldName(Destination.Utopia)).Replace("{anti_utopia}", GetWorldName(Destination.AntiUtopia));
        }
        private string RenderFeedback(string text, ApprovalRecord a, DayEndReport r)
        {
            return text.Replace("{day}", a.day.ToString()).Replace("{name}", a.npcName).Replace("{choice}", GetChoiceName(a.decision))
                .Replace("{source}", GetWorldName(a.sourceWorld)).Replace("{destination}", GetWorldName(a.declaredDestination))
                .Replace("{purityDelta}", Number(a.purityDelta)).Replace("{orderDelta}", Number(a.orderDelta))
                .Replace("{actualPurityDelta}", Number(r.actualPurityChange)).Replace("{actualOrderDelta}", Number(r.actualOrderChange))
                .Replace("{utopia}", GetWorldName(Destination.Utopia)).Replace("{anti_utopia}", GetWorldName(Destination.AntiUtopia));
        }
        private float SettleWorld(float previous, decimal change)
        {
            if (!config.enableWorldChanges) return previous;
            decimal rounded = decimal.Round((decimal)previous + change, 0, MidpointRounding.AwayFromZero);
            return (float)Math.Max((decimal)config.worldValueMin, Math.Min((decimal)config.worldValueMax, rounded));
        }
        private bool Formula(string id) { return config.formulas.Any(f => f.id == id && f.enabled && f.confirmed); }
        private static Decision ReleaseChoice(Destination d) { return d == Destination.Utopia ? Decision.ReleaseToUtopia : Decision.ReleaseToAntiUtopia; }
        private static DecisionResult Rejected(string e) { return new DecisionResult { error = e }; }
        private static string Number(float value) { return value.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture); }
        private NpcData Find(string id) { return state.npcs.SingleOrDefault(n => n.id == id); }
        private string Detail(string axis, int value) { return config.details.Single(d => d.attribute == axis && d.level == GetLevel(value)).text; }
        private TagDefinition Tag(string x, string y, NpcAttributes a)
        { return config.tags.Single(t => t.axisX == x && t.axisY == y && t.xLevel == GetLevel(a.Get(x)) && t.yLevel == GetLevel(a.Get(y))).Copy<TagDefinition>(); }
    }
}
