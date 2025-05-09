using Newtonsoft.Json;
using System;
using Silkroad;
using System.Collections.Generic;

public class DeliverySaveData
{
    public string ProductID;
    public uint RequiredAmount;
    public string DeliveryDropGUID;
    public string RewardDropGUID;
    public bool Initialized;
    public string DealerName;
    public string? QuestImage;
    public string Task;
    public Drug RequiredDrug;
    public int Reward;
    public int RepReward;
    public int XpReward;


    // For calculating rewards
    public float RepMult;
    public float XpMult;
    public int DealTime;
    public float DealTimeMult;
    public List<int> Penalties; // Money and Rep Penalties for failing Deal
    public List<string> OptionalEffects;
    public List<string> NecessaryEffects;
    public string Quality;

    public DeliverySaveData()
    {
        Penalties = new List<int>(); // Initialize the list to avoid null reference
        OptionalEffects = new List<string>();
        NecessaryEffects = new List<string>();
    }

}
