using System;
using System.Collections.Generic;
using System.Linq;

namespace JU.GameData
{
    /// <summary>独立游戏逻辑。所有公开查询均返回副本，不依赖现有UI或时钟。</summary>
    public sealed class GameSession
    {
        private readonly GameConfigData config;
        private readonly Random random;
        private readonly GameState state;
        private int nextNpcId;
        private GameSession(GameConfigData config, int seed)
        {
            var errors = ConfigValidator.Validate(config);
            if (errors.Count != 0) throw new ArgumentException(string.Join("\n", errors), "config");
            this.config = config.Copy();
            random = new Random(seed);
            state = new GameState { day = config.startDay, seed = seed, trust = config.initialTrust,
                purity = config.initialPurity, order = config.initialOrder };
            GenerateApplicants();
        }
        public static GameSession StartNewGame(GameConfigData config, int seed)
        { return new GameSession(config, seed); }
        public GameState GetState() { return state.Copy(); }
        public NpcData GetNpc(string id)
        { var npc = Find(id); return npc == null ? null : npc.Copy(); }
        public NpcProfile GetProfile(string id)
        {
            var n = Find(id);
            if (n == null) return null;
            return new NpcProfile { id = n.id, name = n.name, realOrigin = n.realOrigin, motive = n.motive,
                mutated = n.mutated, memoryText = Detail("memory", n.attributes.memory),
                relationshipText = Detail("relationship", n.attributes.relationship),
                desireText = Detail("desire", n.attributes.desire), willText = Detail("will", n.attributes.will),
                bodyText = Detail("desire_body", n.attributes.desire),
                bondTag = Tag("memory", "relationship", n.attributes).Copy<TagDefinition>(),
                driveTag = Tag("desire", "will", n.attributes).Copy<TagDefinition>() };
        }
        public TravelDocument GetDocument(string id)
        {
            var n = Find(id);
            return n == null ? null : new TravelDocument { id = n.id, name = n.name,
                declaredOrigin = n.declaredOrigin, requestedDestination = n.requestedDestination,
                bodyText = n.bodyDeclaration, reasonText = n.reasonDeclaration };
        }
        public int GetLevel(int value)
        {
            if (value < -10 || value > 10) throw new ArgumentOutOfRangeException("value");
            return config.bands.Single(x => value >= x.minValue && value <= x.maxValue).level;
        }
        public static int CalculateDeviation(NpcAttributes a, Destination destination)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (!Enum.IsDefined(typeof(Destination), destination)) throw new ArgumentOutOfRangeException("destination");
            return Math.Max(0, -a.memory) + Math.Max(0, destination == Destination.Utopia ? -a.relationship : a.relationship)
                + Math.Max(0, a.desire) + Math.Max(0, a.will);
        }
        public Deception GetDeception(int deviation)
        {
            if (deviation < 0) throw new ArgumentOutOfRangeException("deviation");
            return deviation <= config.honestMaxDeviation ? Deception.Honest :
                deviation <= config.embellishedMaxDeviation ? Deception.Embellished : Deception.Deceptive;
        }
        public static float CalculateImpact(NpcAttributes a, Destination destination)
        {
            if (a == null) throw new ArgumentNullException("a");
            if (!Enum.IsDefined(typeof(Destination), destination)) throw new ArgumentOutOfRangeException("destination");
            return (a.memory + (destination == Destination.Utopia ? a.relationship : -a.relationship) - a.desire - a.will) / 10f;
        }
        public DecisionResult SubmitDecision(string npcId, Decision decision)
        {
            if (state.gameOver) return Rejected("游戏已结束。");
            if (!Enum.IsDefined(typeof(Decision), decision)) return Rejected("未知审批操作。");
            var npc = Find(npcId);
            if (npc == null) return Rejected("NPC不存在。");
            if (npc.status != NpcStatus.Pending && npc.status != NpcStatus.Sheltered) return Rejected("该NPC已完成审批或已离开。");
            if (decision == Decision.Reject)
            {
                if (npc.status == NpcStatus.Sheltered) return Rejected("该NPC已在收容所。");
                npc.status = NpcStatus.Sheltered;
                return new DecisionResult { success = true, npc = npc.Copy() };
            }
            if (npc.mutated) return Rejected("变异NPC不能正常放行；本版未实现解除变异。");
            var destination = decision == Decision.ReleaseToUtopia ? Destination.Utopia : Destination.AntiUtopia;
            var impact = CalculateImpact(npc.attributes, destination);
            var approval = new ApprovalRecord { npcId = npc.id, day = state.day, decision = decision,
                worldImpact = impact, compliant = impact >= 0 };
            npc.status = NpcStatus.Released;
            npc.releasedDestination = destination;
            state.todayApprovals.Add(approval);
            return new DecisionResult { success = true, approval = approval.Copy(), npc = npc.Copy() };
        }
        public DayEndReport EndDay(int expectedDay)
        {
            if (state.gameOver) return new DayEndReport { error = "游戏已结束。" };
            if (expectedDay != state.day) return new DayEndReport { error = "天数不匹配；请勿重复结算。" };
            var report = new DayEndReport { success = true, settledDay = state.day, before = state.Copy() };
            foreach (var record in state.todayApprovals)
            {
                if (record.decision == Decision.ReleaseToUtopia) report.approvalPurityChange += record.worldImpact;
                else report.approvalOrderChange += record.worldImpact;
                if (record.compliant) report.compliantCount++; else report.violationCount++;
            }
            state.purity += report.approvalPurityChange;
            state.order += report.approvalOrderChange;
            bool wasActive = state.blackSwanRemaining > 0;
            foreach (var n in state.npcs.Where(x => x.status == NpcStatus.Sheltered))
            {
                n.attributes.memory -= config.memoryDecay;
                n.attributes.relationship -= config.relationshipDecay;
                n.attributes.desire += config.desireGrowth + (wasActive ? config.blackSwanExtraDesire : 0);
                n.attributes.will += config.willGrowth + (wasActive ? config.blackSwanExtraWill : 0);
                n.attributes.Clamp();
                if (!n.mutated && n.attributes.desire >= 10 && n.attributes.will >= 10)
                { n.mutated = true; report.newlyMutatedIds.Add(n.id); }
            }
            if (wasActive)
            {
                report.blackSwanApplied = true;
                report.eventDamage = config.blackSwanDamage;
                state.purity -= config.blackSwanDamage;
                state.order -= config.blackSwanDamage;
                state.blackSwanRemaining--;
            }
            report.purityRecovery = Recovery(state.purity);
            report.orderRecovery = Recovery(state.order);
            state.purity += report.purityRecovery;
            state.order += report.orderRecovery;
            report.trustChange = report.compliantCount * config.complianceReward - report.violationCount * config.violationPenalty + config.dailyTrustBonus;
            state.trust += report.trustChange;
            if (state.trust <= 0) state.failureReasons.Add("信任值≤0，边检官被解雇。");
            if (state.purity <= config.worldFailureThreshold) state.failureReasons.Add("纯净值达到崩溃阈值。");
            if (state.order <= config.worldFailureThreshold) state.failureReasons.Add("秩序值达到崩溃阈值。");
            state.gameOver = state.failureReasons.Count > 0;
            if (!state.gameOver && !wasActive && (state.purity <= config.blackSwanThreshold || state.order <= config.blackSwanThreshold))
            { state.blackSwanRemaining = config.blackSwanDays; report.blackSwanStarted = true; }
            foreach (var n in state.npcs.Where(x => x.status == NpcStatus.Pending)) n.status = NpcStatus.Departed;
            if (!state.gameOver)
            {
                // 只保留收容者与新申请者。当天离开/放行者已记录在结算快照，避免长期游玩无限增长。
                state.npcs.RemoveAll(x => x.status != NpcStatus.Sheltered);
                state.todayApprovals.Clear();
                state.day++;
                GenerateApplicants();
            }
            report.after = state.Copy();
            return report;
        }
        private float Recovery(float value)
        { return value < config.recoveryCeiling ? Math.Min(config.dailyRecovery, config.recoveryCeiling - value) : 0; }
        private NpcData Find(string id) { return state.npcs.FirstOrDefault(x => x.id == id); }
        private static DecisionResult Rejected(string reason) { return new DecisionResult { error = reason }; }
        private string Detail(string axis, int value)
        { return config.details.Single(x => x.attribute == axis && x.level == GetLevel(value)).text; }
        private TagDefinition Tag(string x, string y, NpcAttributes a)
        { return config.tags.Single(t => t.axisX == x && t.axisY == y && t.xLevel == GetLevel(a.Get(x)) && t.yLevel == GetLevel(a.Get(y))); }
        private int Roll()
        {
            double roll = random.NextDouble(), cumulative = 0;
            var bands = config.bands.OrderBy(x => x.level).ToList();
            var chosen = bands[bands.Count - 1];
            foreach (var band in bands)
            { cumulative += band.weight; if (roll < cumulative) { chosen = band; break; } }
            return random.Next(chosen.minValue, chosen.maxValue + 1);
        }
        private void GenerateApplicants()
        {
            for (int i = 0; i < config.dailyApplicants; i++)
            {
                var a = new NpcAttributes { memory = Roll(), relationship = Roll(), desire = Roll(), will = Roll() };
                var matches = config.motives.Where(m => m.attribute != "any" && GetLevel(a.Get(m.attribute)) == m.level).OrderBy(m => m.id).ToList();
                var motive = matches.Count == 0 ? config.motives.Single(m => m.attribute == "any") : matches[random.Next(matches.Count)];
                var destination = (Destination)random.Next(2);
                int deviation = CalculateDeviation(a, destination);
                var deception = GetDeception(deviation);
                string origin = config.origins[random.Next(config.origins.Count)];
                var otherOrigins = config.origins.Where(x => x != origin).ToList();
                var npc = new NpcData { id = "NPC-" + (++nextNpcId).ToString("D6"), generatedDay = state.day,
                    attributes = a, name = config.names[random.Next(config.names.Count)], realOrigin = origin,
                    declaredOrigin = deception == Deception.Honest ? origin : otherOrigins[random.Next(otherOrigins.Count)],
                    motiveId = motive.id, motive = motive.name, requestedDestination = destination, deviation = deviation,
                    deception = deception, status = NpcStatus.Pending, mutated = a.desire >= 10 && a.will >= 10 };
                npc.bodyDeclaration = deception == Deception.Honest ? Detail("desire_body", a.desire) : Document("body_state", npc);
                npc.reasonDeclaration = Document("entry_reason", npc);
                state.npcs.Add(npc);
            }
        }
        private string Document(string field, NpcData n)
        {
            string destination = n.requestedDestination == Destination.Utopia ? "utopia" : "anti_utopia";
            var row = config.documents.Where(x => x.field == field && x.triggerType == (int)n.deception &&
                (x.motive == "any" || x.motive == n.motive) && (x.destination == "any" || x.destination == destination))
                .OrderByDescending(x => (x.motive == "any" ? 0 : 2) + (x.destination == "any" ? 0 : 1)).ThenBy(x => x.id).First();
            // 定义固定占位符，禁止执行文书中的任何指令。
            return row.text.Replace("{motive}", n.motive).Replace("{motive_desc}", config.motives.Single(x => x.id == n.motiveId).description)
                .Replace("{destination}", n.requestedDestination == Destination.Utopia ? "乌托邦" : "反乌托邦")
                .Replace("{name}", n.name).Replace("{origin}", n.declaredOrigin);
        }
    }
}
