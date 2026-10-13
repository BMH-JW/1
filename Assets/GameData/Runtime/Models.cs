using System;
using System.Collections.Generic;
using System.Linq;

namespace JU.GameData
{
    public enum Destination { Utopia, AntiUtopia }
    public enum Decision { ReleaseToUtopia, ReleaseToAntiUtopia, Reject }
    public enum NpcStatus { Pending, Sheltered, Released, Departed }
    public enum Deception { Honest, Embellished, Deceptive }

    [Serializable]
    public abstract class ConfigRow
    {
        public T Copy<T>() where T : ConfigRow { return (T)MemberwiseClone(); }
    }

    [Serializable] public class AttributeBand : ConfigRow
    {
        public int level, minValue, maxValue;
        public float weight;
        public string description;
    }
    [Serializable] public class TagDefinition : ConfigRow
    {
        public int id, xLevel, yLevel;
        public string name, axisX, axisY, description;
    }
    [Serializable] public class DetailDefinition : ConfigRow
    {
        public int id, level;
        public string attribute, text, description;
    }
    [Serializable] public class MotiveDefinition : ConfigRow
    {
        public int id, level;
        public string attribute, name, description;
    }
    [Serializable] public class DocumentDefinition : ConfigRow
    {
        public int id, triggerType;
        public string field, motive, destination, text, source;
    }
    [Serializable] public class ItemDefinition : ConfigRow
    {
        public string id, name, type, costDescription, target, effectType;
        public string originalEffect, formulaEffect, acquisition, notes;
        public int crystalCost = -1;
    }

    [Serializable]
    public class GameConfigData
    {
        public int startDay = 1, dailyApplicants = 8, initialTrust = 50;
        public float initialPurity = 50, initialOrder = 50;
        public float recoveryCeiling = 50, dailyRecovery = 1;
        public float blackSwanThreshold = 30, worldFailureThreshold = 20;
        public int blackSwanDays = 3;
        public float blackSwanDamage = 10;
        public int complianceReward = 5, violationPenalty = 10, dailyTrustBonus = 5;
        public int memoryDecay = 1, relationshipDecay = 1, desireGrowth = 1, willGrowth = 1;
        public int blackSwanExtraDesire = 2, blackSwanExtraWill = 2;
        public int honestMaxDeviation = 3, embellishedMaxDeviation = 8;
        public List<AttributeBand> bands = new List<AttributeBand>();
        public List<TagDefinition> tags = new List<TagDefinition>();
        public List<DetailDefinition> details = new List<DetailDefinition>();
        public List<MotiveDefinition> motives = new List<MotiveDefinition>();
        public List<DocumentDefinition> documents = new List<DocumentDefinition>();
        public List<ItemDefinition> items = new List<ItemDefinition>();
        public List<string> names = new List<string>();
        public List<string> origins = new List<string>();

        public GameConfigData Copy()
        {
            var copy = (GameConfigData)MemberwiseClone();
            copy.bands = bands.Select(x => x.Copy<AttributeBand>()).ToList();
            copy.tags = tags.Select(x => x.Copy<TagDefinition>()).ToList();
            copy.details = details.Select(x => x.Copy<DetailDefinition>()).ToList();
            copy.motives = motives.Select(x => x.Copy<MotiveDefinition>()).ToList();
            copy.documents = documents.Select(x => x.Copy<DocumentDefinition>()).ToList();
            copy.items = items.Select(x => x.Copy<ItemDefinition>()).ToList();
            copy.names = new List<string>(names);
            copy.origins = new List<string>(origins);
            return copy;
        }
    }

    [Serializable]
    public class NpcAttributes
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
        public void Clamp()
        {
            memory = Bound(memory); relationship = Bound(relationship);
            desire = Bound(desire); will = Bound(will);
        }
        private static int Bound(int value) { return Math.Max(-10, Math.Min(10, value)); }
    }

    [Serializable]
    public class NpcData
    {
        public string id, name, realOrigin, declaredOrigin;
        public NpcAttributes attributes;
        public int generatedDay, motiveId, deviation;
        public string motive, bodyDeclaration, reasonDeclaration;
        public Destination requestedDestination;
        public Deception deception;
        public NpcStatus status;
        public bool mutated;
        public Destination? releasedDestination;
        public NpcData Copy()
        {
            var copy = (NpcData)MemberwiseClone();
            copy.attributes = attributes.Copy();
            return copy;
        }
    }

    public class NpcProfile
    {
        public string id, name, realOrigin, motive;
        public string memoryText, relationshipText, desireText, willText, bodyText;
        public TagDefinition bondTag, driveTag;
        public bool mutated;
    }
    public class TravelDocument
    {
        public string id, name, declaredOrigin, bodyText, reasonText;
        public Destination requestedDestination;
    }
    [Serializable]
    public class ApprovalRecord
    {
        public string npcId;
        public int day;
        public Decision decision;
        public float worldImpact;
        public bool compliant;
        public ApprovalRecord Copy() { return (ApprovalRecord)MemberwiseClone(); }
    }
    public class DecisionResult
    {
        public bool success;
        public string error;
        public ApprovalRecord approval;
        public NpcData npc;
    }
    public class GameState
    {
        public int day, seed, trust, blackSwanRemaining;
        public float purity, order;
        public bool gameOver;
        public List<string> failureReasons = new List<string>();
        public List<NpcData> npcs = new List<NpcData>();
        public List<ApprovalRecord> todayApprovals = new List<ApprovalRecord>();
        public GameState Copy()
        {
            var copy = (GameState)MemberwiseClone();
            copy.failureReasons = new List<string>(failureReasons);
            copy.npcs = npcs.Select(x => x.Copy()).ToList();
            copy.todayApprovals = todayApprovals.Select(x => x.Copy()).ToList();
            return copy;
        }
    }
    public class DayEndReport
    {
        public bool success;
        public string error;
        public int settledDay, compliantCount, violationCount, trustChange;
        public float approvalPurityChange, approvalOrderChange;
        public float purityRecovery, orderRecovery, eventDamage;
        public bool blackSwanApplied, blackSwanStarted;
        public List<string> newlyMutatedIds = new List<string>();
        public GameState before, after;
    }
}
