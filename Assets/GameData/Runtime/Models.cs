using System;
using System.Collections.Generic;
using System.Linq;

namespace JU.GameData
{
    // 保留原有编号；拒签不再等于收容。旧Sheltered编号仅用于识别旧调用，不参与本版流程。
    public enum Destination { Utopia, AntiUtopia }
    public enum Decision { ReleaseToUtopia, ReleaseToAntiUtopia, Reject, Detain }
    public enum NpcStatus { Pending, Sheltered, Released, Departed, Rejected }
    public enum Deception { Honest, Embellished, Deceptive }
    public enum TieBreakPolicy { Unconfigured, Reject, Low, High }
    public enum RuleConflictPolicy { RequireAgreement, HighestPriority }
    [Serializable] public abstract class ConfigRow
    { public T Copy<T>() where T : ConfigRow { return (T)MemberwiseClone(); } }
    [Serializable] public class WorldDefinition : ConfigRow
    { public Destination id; public string displayName; }
    [Serializable] public class AttributeBand : ConfigRow
    { public int level, minValue, maxValue; public string description; }
    [Serializable] public class GenerationWeight : ConfigRow
    { public int level; public float weight; }
    [Serializable] public class SemanticDefinition : ConfigRow
    { public string attribute, meaning; public int level; public float score; public bool confirmed; }
    [Serializable] public class DailyRule : ConfigRow
    {
        public string id, title, attribute, source;
        public bool enabled, confirmed;
        public int firstDay = 1, lastDay, repeatEveryDays = 1, priority;
        public float lowAnchor, highAnchor;
        public Decision lowDecision, highDecision;
        public TieBreakPolicy tieBreak;
        public bool unmatchedReject;
        public DailyRule Copy() { return Copy<DailyRule>(); }
    }
    [Serializable] public class FormulaDefinition : ConfigRow
    { public string id, description, source; public bool enabled, confirmed, testOnly; }
    [Serializable] public class TagDefinition : ConfigRow
    { public int id, xLevel, yLevel; public string name, axisX, axisY, description; }
    [Serializable] public class DetailDefinition : ConfigRow
    { public int id, level; public string attribute, text, description; }
    [Serializable] public class MotiveDefinition : ConfigRow
    { public int id, level; public string attribute, name, description; }
    [Serializable] public class DocumentDefinition : ConfigRow
    { public int id, triggerType; public string field, motive, destination, text, source; }
    [Serializable] public class ItemDefinition : ConfigRow
    {
        public string id, name, type, costDescription, target, effectType;
        public string originalEffect, formulaEffect, acquisition, notes;
        public int crystalCost = -1;
    }
    [Serializable] public class OriginPortraitPool : ConfigRow
    {
        public string origin;
        public List<string> portraitKeys = new List<string>();
        public OriginPortraitPool Copy()
        { var c = Copy<OriginPortraitPool>(); c.portraitKeys = new List<string>(portraitKeys); return c; }
    }
    [Serializable] public class NpcIdentityDefinition : ConfigRow
    {
        public string id, name, origin, portraitKey, source;
        public bool sampleOnly, useFixedAttributes;
        public NpcAttributes fixedAttributes = new NpcAttributes();
        // F003关闭后由样本显式指定，不根据属性或旧偏差公式推导。
        public Deception declarationType;
        public int motiveId;
        public NpcIdentityDefinition Copy()
        { var c = Copy<NpcIdentityDefinition>(); c.fixedAttributes = fixedAttributes.Copy(); return c; }
    }
    [Serializable] public class IdentitySnapshot : ConfigRow
    { public string id, name, origin, portraitKey; }
    [Serializable] public class FeedbackTemplate : ConfigRow
    {
        public string id, text, source;
        public Decision decision;
        public int compliance = -1; // -1不限，0违规，1合规
        public bool testOnly;
    }
    [Serializable] public class GameConfigData
    {
        public string version = "v3-reply-test", sourceWorkbook, sourceSha256, replyDocument, replySha256;
        public bool testConfiguration = true;
        public int startDay = 1, minDailyApplicants = 3, maxDailyApplicants = 6;
        public int initialTrust = 50, trustMin = 0, trustMax = 100;
        public float initialPurity = 50, initialOrder = 50, worldValueMin = 0, worldValueMax = 100;
        public int complianceReward = 3, violationPenalty = 5;
        public int impactDivisor = 5, rejectionSourcePenalty = 1;
        public bool enableDailyRules = true, enableBondTrace = true, enableDriveAssessment = true;
        public bool enableDocuments = true, enableWorldChanges = true, enableDelayedFeedback = true;
        public bool showImmediateCompliance; // 默认仅回执，不提前展示答案。
        public bool enablePopulationFeedback, enableThresholdEvents; // 未定方案不可启用，校验会拦截。
        public bool debugMode, logRuleResult;
        public int feedbackDelayDays = 1, dailyRuleCount = 1;
        public string bondTraceLabel = "羁绊牵挂", driveAssessmentLabel = "驱力等级";
        public RuleConflictPolicy conflictPolicy;
        public List<WorldDefinition> worlds = new List<WorldDefinition>();
        public List<AttributeBand> bands = new List<AttributeBand>();
        public List<GenerationWeight> generationWeights = new List<GenerationWeight>();
        public List<SemanticDefinition> semantics = new List<SemanticDefinition>();
        public List<DailyRule> rules = new List<DailyRule>();
        public List<FormulaDefinition> formulas = new List<FormulaDefinition>();
        public List<TagDefinition> tags = new List<TagDefinition>();
        public List<DetailDefinition> details = new List<DetailDefinition>();
        public List<MotiveDefinition> motives = new List<MotiveDefinition>();
        public List<DocumentDefinition> documents = new List<DocumentDefinition>();
        public List<ItemDefinition> items = new List<ItemDefinition>();
        public List<NpcIdentityDefinition> identities = new List<NpcIdentityDefinition>();
        public List<OriginPortraitPool> portraitPools = new List<OriginPortraitPool>();
        public List<FeedbackTemplate> feedbackTemplates = new List<FeedbackTemplate>();
        public List<float> lightFeedbackThresholds = new List<float> { 45, 55 };
        public List<float> formalEventThresholds = new List<float> { 40, 60 };
        public List<string> implementationDecisions = new List<string>();
        public List<string> pendingDecisions = new List<string>();
        public GameConfigData Copy()
        {
            var c = (GameConfigData)MemberwiseClone();
            c.worlds = worlds.Select(x => x.Copy<WorldDefinition>()).ToList();
            c.bands = bands.Select(x => x.Copy<AttributeBand>()).ToList();
            c.generationWeights = generationWeights.Select(x => x.Copy<GenerationWeight>()).ToList();
            c.semantics = semantics.Select(x => x.Copy<SemanticDefinition>()).ToList();
            c.rules = rules.Select(x => x.Copy()).ToList();
            c.formulas = formulas.Select(x => x.Copy<FormulaDefinition>()).ToList();
            c.tags = tags.Select(x => x.Copy<TagDefinition>()).ToList();
            c.details = details.Select(x => x.Copy<DetailDefinition>()).ToList();
            c.motives = motives.Select(x => x.Copy<MotiveDefinition>()).ToList();
            c.documents = documents.Select(x => x.Copy<DocumentDefinition>()).ToList();
            c.items = items.Select(x => x.Copy<ItemDefinition>()).ToList();
            c.identities = identities.Select(x => x.Copy()).ToList();
            c.portraitPools = portraitPools.Select(x => x.Copy()).ToList();
            c.feedbackTemplates = feedbackTemplates.Select(x => x.Copy<FeedbackTemplate>()).ToList();
            c.lightFeedbackThresholds = new List<float>(lightFeedbackThresholds);
            c.formalEventThresholds = new List<float>(formalEventThresholds);
            c.implementationDecisions = new List<string>(implementationDecisions);
            c.pendingDecisions = new List<string>(pendingDecisions);
            return c;
        }
    }
    [Serializable] public class NpcAttributes
    {
        public int memory, relationship, desire, will;
        public int Get(string axis)
        {
            switch (axis)
            {
                case "memory": return memory;
                case "relationship": return relationship;
                case "desire": case "desire_body": return desire;
                case "will": return will;
                default: throw new ArgumentException("未知属性：" + axis);
            }
        }
        public NpcAttributes Copy() { return (NpcAttributes)MemberwiseClone(); }
    }
    [Serializable] public class NpcData
    {
        public string id, identityId, name, realOrigin, declaredOrigin, portraitKey;
        public Destination sourceWorld, requestedDestination;
        public NpcAttributes attributes;
        public int generatedDay, motiveId;
        public string motive, bodyDeclaration, reasonDeclaration;
        public Deception deception;
        public NpcStatus status;
        public Destination? releasedDestination;
        public NpcData Copy() { var c = (NpcData)MemberwiseClone(); c.attributes = attributes.Copy(); return c; }
    }
    public class NpcProfile
    {
        public string id, name, realOrigin, motive;
        public string memoryText, relationshipText, desireText, willText, bodyText;
        public TagDefinition bondTag, driveTag;
    }
    public class TravelDocument
    {
        public string id, name, declaredOrigin, bodyText, reasonText;
        public Destination sourceWorld, requestedDestination;
        public string sourceDisplayName, destinationDisplayName;
    }
    public class PlayerProfile
    {
        public string id, bondTraceSentence, driveAssessmentSentence;
        public List<string> relevantAttributes = new List<string>();
    }
    public class RuleEvaluation
    {
        public bool success, applicable;
        public string error;
        public Decision target; // 记忆准入目标，与实际申报不同。
        public Decision requiredDecision; // 当前申请应执行放行还是拒签。
        public List<string> traces = new List<string>();
    }
    [Serializable] public class ApprovalRecord
    {
        public string id, npcId, identityId, npcName, ruleIds, ruleTrace;
        public int day, memoryAtDecision;
        public bool complianceEvaluated, compliant;
        public Destination sourceWorld, declaredDestination;
        public Decision decision, ruleTarget, requiredDecision;
        public float purityDelta, orderDelta;
        // 账本为内存局；精确累计避免先转float再舍入。未提供跨进程序列化协议。
        internal decimal exactPurityDelta, exactOrderDelta;
        public ApprovalRecord Copy() { return (ApprovalRecord)MemberwiseClone(); }
    }
    public class DecisionResult
    { public bool success; public string error, immediateFeedback; public ApprovalRecord approval; }
    [Serializable] public class FeedbackEvent : ConfigRow
    {
        public string id, templateId, sourceDecisionId, settlementId, text;
        public int settledDay, availableDay;
        public float recordedPurityDelta, recordedOrderDelta, actualPurityChange, actualOrderChange;
        public bool consumed;
    }
    public class GameState
    {
        public int day, seed, trust, todayApplicantCount;
        public float purity, order;
        public List<NpcData> npcs = new List<NpcData>();
        public List<IdentitySnapshot> identities = new List<IdentitySnapshot>();
        public List<ApprovalRecord> todayApprovals = new List<ApprovalRecord>();
        public List<DailyRule> dailyRules = new List<DailyRule>();
        public List<FeedbackEvent> feedbackQueue = new List<FeedbackEvent>();
        public GameState Copy()
        {
            var c = (GameState)MemberwiseClone();
            c.npcs = npcs.Select(x => x.Copy()).ToList();
            c.identities = identities.Select(x => x.Copy<IdentitySnapshot>()).ToList();
            c.todayApprovals = todayApprovals.Select(x => x.Copy()).ToList();
            c.dailyRules = dailyRules.Select(x => x.Copy()).ToList();
            c.feedbackQueue = feedbackQueue.Select(x => x.Copy<FeedbackEvent>()).ToList();
            return c;
        }
    }
    public class DayEndReport
    {
        public bool success;
        public string error, settlementId;
        public int settledDay, compliantCount, violationCount, trustChange, unprocessedCount;
        public float recordedPurityChange, recordedOrderChange, actualPurityChange, actualOrderChange;
        public bool trustAtBoundary, purityAtBoundary, orderAtBoundary;
        public bool gameEnded;
        public bool isWin;
        public string gameEndReason;
        public GameState before, after;
        public List<ApprovalRecord> approvals = new List<ApprovalRecord>();
        public List<string> log = new List<string>();
    }
}
