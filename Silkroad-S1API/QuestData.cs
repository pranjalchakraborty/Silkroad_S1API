using System.Collections.Generic;

namespace SilkRoad
{
    public class QuestData
    {
        public string Title;
        public string Task;
        public int Reward;
        public string ProductID;
        public uint AmountRequired;
        public string TargetObjectName;
        public string DealerName;
        public List<string> NecessaryEffects;
        public List<string> OptionalEffects;
        public string? QuestImage;
    
    }
}