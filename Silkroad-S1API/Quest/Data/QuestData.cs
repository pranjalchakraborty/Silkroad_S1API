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
        public float DealTimeMultiplier;
        public List<int> Penalties; // Money and Rep Penalties for failing Deal

        // No need to save. Only to show Rewards in Journal
        public int BonusDollar;
        public float BonusRep;
        public float DollarMultiplierMin;
        public float RepMultiplierMin;
        public float DollarMultiplierMax;
        public float RepMultiplierMax;

    }
}