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

        // No need to save. Only to show Rewards in Journal
        public int BonusDollar;
        public float BonusRep;
        public float DollarMultiplierMin;
        public float RepMultiplierMin;
        public float DollarMultiplierMax;
        public float RepMultiplierMax;

    }
}