using Newtonsoft.Json;
using System;
using Silkroad;

[Serializable]
public class DeliverySaveData
{
    public string ProductID;
    public uint RequiredAmount;
    public string DeliveryDropGUID;
    public string RewardDropGUID;
    public bool Initialized;
    public string DealerName;
    public string? QuestImage;
    public Drug RequiredDrug;
    public int Reward;
    public float RepReward;
    public string Task;

    [JsonConstructor]
    public DeliverySaveData() { }
}
