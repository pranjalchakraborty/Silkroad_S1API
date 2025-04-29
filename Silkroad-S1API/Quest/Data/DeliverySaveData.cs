using Newtonsoft.Json;
using System;
using Silkroad;
using System.Collections.Generic;

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
    public int DealTime;
    public float DealTimeMult;
    public List<int> Penalties; // Money and Rep Penalties for failing Deal
    
    [JsonConstructor]
    public DeliverySaveData() 
    { 
        Penalties = new List<int>(); // Initialize the list to avoid null reference
    }

}
