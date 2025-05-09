using System.Collections.Generic;
using Silkroad;

namespace Silkroad
{
    public class QuestData
    {
        public string Title;
        public string Task;
        public string ProductID;
        public uint AmountRequired;
        public string TargetObjectName;
        public string DealerName;
        public Drug RequiredDrug;
        public string? QuestImage;
        public int DealTime;
        public float DealTimeMult;
        public List<int> Penalties; // Money and Rep Penalties for failing Deal
        public string Quality;
        public List<string> NecessaryEffects;
        public List<string> OptionalEffects;
        public int BaseDollar;
        public int BaseRep;
        public int BaseXp;
        public float RepMult;
        public float XpMult;
        public float DollarMultiplierMin;
        public float DollarMultiplierMax;
        public int Index;

    }
}